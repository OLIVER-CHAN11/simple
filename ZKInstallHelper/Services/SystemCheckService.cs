using System;
using System.Management;
using System.Security.Principal;
using ZKInstallHelper.Models;
using ZKInstallHelper.Utils;

namespace ZKInstallHelper.Services;

/// <summary>
/// 系统环境检测服务
/// </summary>
public class SystemCheckService
{
    private readonly LogService _log = LogService.Instance;

    public SystemCheckResult RunFullCheck()
    {
        _log.LogTo("system_check.log", "========== 开始系统环境检测 ==========");
        var result = new SystemCheckResult();

        // Windows 版本
        result.WindowsVersion = Environment.OSVersion.VersionString;
        result.WindowsBuild = Environment.OSVersion.Version.Build.ToString();
        _log.LogTo("system_check.log", $"Windows 版本: {result.WindowsVersion}");
        _log.LogTo("system_check.log", $"Build 号: {result.WindowsBuild}");

        // 64 位
        result.Is64Bit = Environment.Is64BitOperatingSystem;
        _log.LogTo("system_check.log", $"64 位系统: {result.Is64Bit}");

        // CPU
        result.CpuInfo = GetCpuInfo();
        _log.LogTo("system_check.log", $"CPU: {result.CpuInfo}");

        // 内存
        result.MemoryGB = GetTotalMemoryGB();
        _log.LogTo("system_check.log", $"内存: {result.MemoryGB:F1} GB");

        // 磁盘
        result.CDriveFreeGB = FileHelper.GetDriveFreeSpaceGB("C");
        _log.LogTo("system_check.log", $"C 盘剩余: {result.CDriveFreeGB:F1} GB");

        result.DDriveFreeGB = FileHelper.GetDriveFreeSpaceGB("D");
        if (result.DDriveFreeGB >= 0)
            _log.LogTo("system_check.log", $"D 盘剩余: {result.DDriveFreeGB:F1} GB");
        else
            _log.LogTo("system_check.log", "D 盘: 不存在");

        // 用户
        result.UserName = Environment.UserName;
        result.UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        result.UserPathContainsChinese = FileHelper.ContainsChinese(result.UserProfilePath);
        _log.LogTo("system_check.log", $"用户名: {result.UserName}");
        _log.LogTo("system_check.log", $"用户目录: {result.UserProfilePath}");
        _log.LogTo("system_check.log", $"用户路径含中文: {result.UserPathContainsChinese}");

        // 程序路径
        var appPath = AppDomain.CurrentDomain.BaseDirectory;
        result.AppPathContainsChinese = FileHelper.ContainsChinese(appPath);
        _log.LogTo("system_check.log", $"程序路径: {appPath}");
        _log.LogTo("system_check.log", $"程序路径含中文: {result.AppPathContainsChinese}");

        // 临时目录
        result.TempPath = Path.GetTempPath();
        _log.LogTo("system_check.log", $"临时目录: {result.TempPath}");

        // 管理员权限
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        result.IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        _log.LogTo("system_check.log", $"管理员权限: {result.IsAdmin}");

        // VC++ 运行库
        var vcService = new VCRuntimeService();
        result.VCx64 = vcService.CheckRuntime("x64");
        result.VCx86 = vcService.CheckRuntime("x86");
        _log.LogTo("system_check.log", result.VCx64.StatusText);
        _log.LogTo("system_check.log", result.VCx86.StatusText);

        // Siemens 日志目录
        result.SiemensLogDirExists = Directory.Exists(result.SiemensLogDir);
        _log.LogTo("system_check.log", $"Siemens 日志目录存在: {result.SiemensLogDirExists}");

        // 杀毒软件
        result.DetectedAntivirus = ProcessHelper.DetectAntivirusProcesses();
        if (result.DetectedAntivirus.Count > 0)
            _log.LogTo("system_check.log", $"检测到杀毒软件: {string.Join(", ", result.DetectedAntivirus)}");
        else
            _log.LogTo("system_check.log", "未检测到常见杀毒软件进程");

        _log.LogTo("system_check.log", "========== 系统环境检测完成 ==========");
        return result;
    }

    /// <summary>格式化检测结果为 UI 显示文本</summary>
    public string FormatResult(SystemCheckResult r)
    {
        var lines = new List<string>
        {
            "━━━━━━━━━━ 系统环境检测结果 ━━━━━━━━━━",
            "",
            r.Is64Bit ? "✅ Windows 64 位系统：支持" : "❌ 非 64 位系统：不支持",
            $"✅ Windows 版本：{r.WindowsVersion} (Build {r.WindowsBuild})",
            r.IsAdmin ? "✅ 管理员权限：正常" : "❌ 管理员权限：异常",
            $"✅ CPU：{r.CpuInfo}",
            $"✅ 内存：{r.MemoryGB:F1} GB",
            "",
            r.CDriveFreeGB >= 50
                ? $"✅ C 盘剩余空间：{r.CDriveFreeGB:F1} GB"
                : $"⚠️ C 盘空间不足：{r.CDriveFreeGB:F1} GB（建议预留 50GB 以上）",
        };

        if (r.DDriveFreeGB >= 0)
            lines.Add($"✅ D 盘剩余空间：{r.DDriveFreeGB:F1} GB");

        lines.Add("");
        lines.Add(r.VCx64.StatusText);
        lines.Add(r.VCx86.StatusText);
        lines.Add("");

        if (r.UserPathContainsChinese)
            lines.Add("⚠️ 当前用户名包含中文，可能影响部分安装程序");
        else
            lines.Add("✅ 用户路径：正常（无中文）");

        if (r.AppPathContainsChinese)
            lines.Add("⚠️ 当前程序路径包含中文，建议放到英文路径");
        else
            lines.Add("✅ 程序路径：正常（无中文）");

        lines.Add("");
        lines.Add(r.SiemensLogDirExists
            ? "✅ Siemens 安装日志目录：存在"
            : "ℹ️ Siemens 安装日志目录：不存在（可能尚未安装）");

        if (r.DetectedAntivirus.Count > 0)
        {
            lines.Add("");
            lines.Add($"⚠️ 检测到杀毒软件正在运行：{string.Join("、", r.DetectedAntivirus)}");
            lines.Add("   建议安装前临时退出杀毒软件，避免误拦截安装文件。");
        }

        lines.Add("");
        lines.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        return string.Join(Environment.NewLine, lines);
    }

    private string GetCpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                return obj["Name"]?.ToString() ?? "未知";
            }
        }
        catch { }
        return "未知";
    }

    private double GetTotalMemoryGB()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out var bytes))
                    return bytes / (1024.0 * 1024 * 1024);
            }
        }
        catch { }
        return 0;
    }
}
