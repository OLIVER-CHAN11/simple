using System.Text;
using ZKSystemPrep.Models;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// 系统准备报告导出服务
/// </summary>
public class ReportService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>生成系统准备报告</summary>
    public string GenerateReport(
        SystemCompatibilityResult? sysResult,
        List<AntivirusProcessItem>? avItems,
        DotNetEnableResult? dotnetResult,
        RebootFixResult? rebootResult,
        UninstallToolResult? uninstallResult,
        bool winSecurityOpened)
    {
        var reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reports");
        FileHelper.EnsureDirectory(reportDir);

        var fileName = $"系统准备报告_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(reportDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║        智控安装助手 - 系统准备报告                        ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"软件版本: 1.0.0");
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"管理员运行: {AdminService.IsRunAsAdmin()}");
        sb.AppendLine();

        // 系统信息
        sb.AppendLine("【系统兼容性信息】");
        if (sysResult != null)
        {
            sb.AppendLine($"  Windows 版本: {sysResult.WindowsVersion}");
            sb.AppendLine($"  Build 号: {sysResult.WindowsBuild}");
            sb.AppendLine($"  Edition: {sysResult.WindowsEdition}");
            sb.AppendLine($"  系统位数: {(sysResult.Is64Bit ? "64 位" : "32 位")}");
            sb.AppendLine($"  CPU: {sysResult.CpuName}");
            sb.AppendLine($"  CPU 核心数: {sysResult.CpuCores}");
            sb.AppendLine($"  内存: {sysResult.MemoryGB:F1} GB");
            sb.AppendLine($"  系统盘剩余: {sysResult.SystemDriveFreeGB:F1} GB");
            sb.AppendLine($"  网络适配器: {sysResult.NetworkAdapters}");
            sb.AppendLine($"  分辨率: {sysResult.ScreenWidth}x{sysResult.ScreenHeight}");
            sb.AppendLine($"  用户名: {sysResult.UserName}");
            sb.AppendLine($"  用户目录含中文: {sysResult.UserPathContainsChinese}");
            sb.AppendLine($"  程序路径含中文: {sysResult.AppPathContainsChinese}");
        }
        else
        {
            sb.AppendLine("  （尚未执行检测）");
        }

        sb.AppendLine();
        sb.AppendLine("【杀毒软件检测】");
        if (avItems != null && avItems.Count > 0)
        {
            var brands = avItems.Select(i => i.BrandName).Distinct();
            foreach (var brand in brands)
            {
                var procs = avItems.Where(i => i.BrandName == brand).Select(i => i.ProcessName).Distinct();
                sb.AppendLine($"  ⚠️ {brand}: {string.Join(", ", procs)}");
            }
        }
        else if (avItems != null)
        {
            sb.AppendLine("  未检测到常见杀毒软件进程");
        }
        else
        {
            sb.AppendLine("  （尚未执行检测）");
        }

        sb.AppendLine();
        sb.AppendLine("【智能应用控制设置】");
        sb.AppendLine($"  设置页面打开: {(winSecurityOpened ? "已打开" : "未操作/打开失败")}");

        sb.AppendLine();
        sb.AppendLine("【.NET Framework 3.5】");
        if (dotnetResult != null)
        {
            sb.AppendLine($"  操作结果: {dotnetResult.Summary}");
            sb.AppendLine($"  ExitCode: {dotnetResult.ExitCode}");
        }
        else
        {
            sb.AppendLine("  （尚未执行）");
        }

        sb.AppendLine();
        sb.AppendLine("【Siemens 重启提示修复】");
        if (rebootResult != null)
        {
            sb.AppendLine($"  PendingFileRenameOperations 存在: {rebootResult.PendingFound}");
            sb.AppendLine($"  已备份: {rebootResult.BackupCreated}");
            if (rebootResult.BackupCreated)
                sb.AppendLine($"  备份路径: {rebootResult.BackupPath}");
            sb.AppendLine($"  已删除: {rebootResult.Deleted}");
            sb.AppendLine($"  摘要: {rebootResult.Summary}");
        }
        else
        {
            sb.AppendLine("  （尚未执行）");
        }

        sb.AppendLine();
        sb.AppendLine("【卸载工具】");
        if (uninstallResult != null)
        {
            sb.AppendLine($"  Geek Uninstaller.exe 存在: {uninstallResult.FileExists}");
            sb.AppendLine($"  路径: {uninstallResult.FilePath}");
            sb.AppendLine($"  启动结果: {(uninstallResult.LaunchSuccess ? "成功" : "未启动/失败")}");
            if (!string.IsNullOrEmpty(uninstallResult.ErrorMessage))
                sb.AppendLine($"  错误: {uninstallResult.ErrorMessage}");
        }
        else
        {
            var toolPath = new UninstallToolService().LocateGeekUninstaller();
            sb.AppendLine($"  Geek Uninstaller.exe 存在: {toolPath != null}");
            sb.AppendLine("  启动结果: 尚未启动");
        }

        sb.AppendLine();
        sb.AppendLine("【最近日志（最后 100 行）】");
        var lastLines = _log.GetLastLines(100);
        foreach (var line in lastLines)
            sb.AppendLine($"  {line}");

        sb.AppendLine();
        sb.AppendLine("【建议下一步操作】");
        var suggestions = GenerateSuggestions(sysResult, avItems, dotnetResult, rebootResult);
        foreach (var s in suggestions)
            sb.AppendLine($"  • {s}");

        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━ 报告结束 ━━━━━━━━━━━━━━━━━━");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        _log.Log($"系统准备报告已生成: {filePath}", LogService.LogLevel.SUCCESS);

        return filePath;
    }

    private List<string> GenerateSuggestions(
        SystemCompatibilityResult? sys,
        List<AntivirusProcessItem>? av,
        DotNetEnableResult? dotnet,
        RebootFixResult? reboot)
    {
        var suggestions = new List<string>();

        if (sys == null)
        {
            suggestions.Add("请先执行"检查系统兼容性"以获取完整建议。");
            return suggestions;
        }

        if (!sys.Is64Bit)
            suggestions.Add("系统不是 64 位，不建议安装 TIA Portal。");

        if (sys.MemoryGB < 8)
            suggestions.Add($"内存仅 {sys.MemoryGB:F1}GB，不满足最低 8GB 要求，不建议安装。");

        if (sys.SystemDriveFreeGB < 50)
            suggestions.Add($"系统盘空间不足（当前 {sys.SystemDriveFreeGB:F1}GB），请先释放至少 50GB 空间。");

        if (av != null && av.Count > 0)
        {
            var brands = av.Select(i => i.BrandName).Distinct().ToList();
            suggestions.Add($"检测到 {string.Join("、", brands)} 正在运行，安装前建议手动退出或暂停防护。");
        }

        if (dotnet != null && !dotnet.Success)
            suggestions.Add(".NET Framework 3.5 启用失败，请检查网络连接或使用 Windows 安装镜像。");

        if (reboot != null && reboot.PendingFound && !reboot.Deleted)
            suggestions.Add("存在 PendingFileRenameOperations，建议优先重启电脑或备份后处理。");

        if (sys.UserPathContainsChinese || sys.AppPathContainsChinese)
            suggestions.Add("用户名或路径含中文，建议将安装包放到英文路径（如 D:\\TIA_Portal\\）。");

        var toolPath = new UninstallToolService().LocateGeekUninstaller();
        if (toolPath != null)
            suggestions.Add("如需卸载旧版本 Siemens 组件，请使用"打开卸载工具"手动处理。");

        if (suggestions.Count == 0)
            suggestions.Add("当前系统环境检测正常，可以继续安装 TIA Portal。");

        return suggestions;
    }
}
