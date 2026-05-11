using System;
using System.Text;
using ZKInstallHelper.Models;

namespace ZKInstallHelper.Services;

/// <summary>
/// 诊断报告生成服务
/// </summary>
public class ReportService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>生成诊断报告</summary>
    public string GenerateReport(SystemCheckResult? checkResult, string? installerDir, string? installerEntry)
    {
        var reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports");
        if (!Directory.Exists(reportDir))
            Directory.CreateDirectory(reportDir);

        var fileName = $"诊断报告_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(reportDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║              智控安装助手 - 诊断报告                     ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"软件版本: 1.0.0");
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("【系统信息】");
        if (checkResult != null)
        {
            sb.AppendLine($"  Windows 版本: {checkResult.WindowsVersion}");
            sb.AppendLine($"  Build 号: {checkResult.WindowsBuild}");
            sb.AppendLine($"  系统位数: {(checkResult.Is64Bit ? "64 位" : "32 位")}");
            sb.AppendLine($"  CPU: {checkResult.CpuInfo}");
            sb.AppendLine($"  内存: {checkResult.MemoryGB:F1} GB");
            sb.AppendLine($"  C 盘剩余: {checkResult.CDriveFreeGB:F1} GB");
            if (checkResult.DDriveFreeGB >= 0)
                sb.AppendLine($"  D 盘剩余: {checkResult.DDriveFreeGB:F1} GB");
            sb.AppendLine($"  用户名: {checkResult.UserName}");
            sb.AppendLine($"  程序路径: {AppDomain.CurrentDomain.BaseDirectory}");
            sb.AppendLine($"  管理员运行: {checkResult.IsAdmin}");
        }
        else
        {
            sb.AppendLine("  （尚未执行环境检测，请先点击"一键检测电脑环境"）");
        }

        sb.AppendLine();
        sb.AppendLine("【VC++ 运行库】");
        if (checkResult != null)
        {
            sb.AppendLine($"  x64: {checkResult.VCx64.StatusText}");
            sb.AppendLine($"  x86: {checkResult.VCx86.StatusText}");
            var vcLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            sb.AppendLine($"  VC++ 安装日志目录: {vcLogDir}");
        }
        else
        {
            sb.AppendLine("  （尚未检测）");
        }

        sb.AppendLine();
        sb.AppendLine("【安装包信息】");
        sb.AppendLine($"  选择的安装包路径: {installerDir ?? "未选择"}");
        sb.AppendLine($"  检测到的安装入口: {installerEntry ?? "未检测"}");

        sb.AppendLine();
        sb.AppendLine("【Siemens 日志】");
        var siemensDir = @"C:\ProgramData\Siemens\Automation\Logfiles\Setup";
        sb.AppendLine($"  日志目录: {siemensDir}");
        sb.AppendLine($"  目录存在: {Directory.Exists(siemensDir)}");

        sb.AppendLine();
        sb.AppendLine("【最近日志（最后 100 行）】");
        var lastLines = _log.GetLastLines(100);
        foreach (var line in lastLines)
            sb.AppendLine($"  {line}");

        sb.AppendLine();
        sb.AppendLine("【建议处理方案】");
        var suggestions = GenerateSuggestions(checkResult, installerDir);
        foreach (var s in suggestions)
            sb.AppendLine($"  • {s}");

        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━ 报告结束 ━━━━━━━━━━━━━━━━━━");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        _log.Log($"诊断报告已生成: {filePath}", LogService.LogLevel.SUCCESS);

        return filePath;
    }

    private List<string> GenerateSuggestions(SystemCheckResult? result, string? installerDir)
    {
        var suggestions = new List<string>();

        if (result == null)
        {
            suggestions.Add("请先执行"一键检测电脑环境"以获取完整建议。");
            return suggestions;
        }

        if (!result.VCx64.Installed || !result.VCx86.Installed)
            suggestions.Add("VC++ 运行库异常，建议点击"一键修复 VC++ 运行库"。");

        if (result.CDriveFreeGB < 50)
            suggestions.Add($"C 盘空间不足（当前 {result.CDriveFreeGB:F1}GB），建议释放 50GB 以上空间。");

        if (result.UserPathContainsChinese || result.AppPathContainsChinese)
            suggestions.Add("用户名或路径包含中文，建议把安装包放到 D:\\TIA_Portal\\ 这类英文路径。");

        if (result.SiemensLogDirExists)
            suggestions.Add("已检测到 Siemens 安装日志，如遇问题建议把日志文件发给售后分析。");

        if (result.DetectedAntivirus.Count > 0)
            suggestions.Add($"检测到 {string.Join("、", result.DetectedAntivirus)} 正在运行，建议安装前临时退出。");

        if (suggestions.Count == 0)
            suggestions.Add("当前环境检测正常，可以正常安装。");

        return suggestions;
    }
}
