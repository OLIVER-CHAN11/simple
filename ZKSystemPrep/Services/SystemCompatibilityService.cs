using System;
using System.Management;
using System.Security.Principal;
using System.Windows.Forms;
using ZKSystemPrep.Models;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// 系统兼容性检测服务
/// </summary>
public class SystemCompatibilityService
{
    private readonly LogService _log = LogService.Instance;

    public SystemCompatibilityResult RunCheck()
    {
        _log.LogTo("system_check.log", "========== 开始系统兼容性检测 ==========");
        var result = new SystemCompatibilityResult();

        // CPU
        result.CpuName = GetWmiValue("Win32_Processor", "Name") ?? "未知";
        var coreStr = GetWmiValue("Win32_Processor", "NumberOfCores");
        result.CpuCores = int.TryParse(coreStr, out var cores) ? cores : 0;
        _log.LogTo("system_check.log", $"CPU: {result.CpuName} ({result.CpuCores} 核)");

        // 内存
        result.MemoryGB = GetTotalMemoryGB();
        result.IsMemorySufficient = result.MemoryGB >= 8;
        _log.LogTo("system_check.log", $"内存: {result.MemoryGB:F1} GB");

        // 系统盘空间
        var sysDrive = FileHelper.GetSystemDriveLetter();
        result.SystemDriveFreeGB = FileHelper.GetDriveFreeSpaceGB(sysDrive);
        result.IsDiskSpaceSufficient = result.SystemDriveFreeGB >= 50;
        _log.LogTo("system_check.log", $"系统盘 {sysDrive}: 剩余 {result.SystemDriveFreeGB:F1} GB");

        // SSD 检测（可选，失败不报错）
        try
        {
            result.SSDDetectable = true;
            result.IsSSD = DetectSSD();
            _log.LogTo("system_check.log", $"SSD: {(result.IsSSD ? "是" : "否/未确定")}");
        }
        catch
        {
            result.SSDDetectable = false;
            _log.LogTo("system_check.log", "SSD: 无法检测");
        }

        // 网络适配器
        result.NetworkAdapters = GetNetworkAdapters();
        _log.LogTo("system_check.log", $"网络适配器: {result.NetworkAdapters}");

        // 显示器分辨率
        var screen = Screen.PrimaryScreen;
        if (screen != null)
        {
            result.ScreenWidth = screen.Bounds.Width;
            result.ScreenHeight = screen.Bounds.Height;
        }
        result.IsResolutionOk = result.ScreenWidth >= 1920 && result.ScreenHeight >= 1080;
        _log.LogTo("system_check.log", $"分辨率: {result.ScreenWidth}x{result.ScreenHeight}");

        // Windows 版本
        result.WindowsVersion = Environment.OSVersion.VersionString;
        result.WindowsBuild = Environment.OSVersion.Version.Build.ToString();
        result.WindowsEdition = GetWindowsEdition();
        result.Is64Bit = Environment.Is64BitOperatingSystem;
        result.IsSystemSupported = result.Is64Bit;
        _log.LogTo("system_check.log", $"Windows: {result.WindowsVersion}");
        _log.LogTo("system_check.log", $"Build: {result.WindowsBuild}");
        _log.LogTo("system_check.log", $"Edition: {result.WindowsEdition}");
        _log.LogTo("system_check.log", $"64 位: {result.Is64Bit}");

        // 版本支持判断
        result.IsVersionSupported = CheckVersionSupported(result.WindowsBuild, result.WindowsEdition);

        // 用户信息
        result.UserName = Environment.UserName;
        result.UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        result.UserPathContainsChinese = FileHelper.ContainsChinese(result.UserProfilePath);
        result.AppPathContainsChinese = FileHelper.ContainsChinese(AppDomain.CurrentDomain.BaseDirectory);
        _log.LogTo("system_check.log", $"用户: {result.UserName}");
        _log.LogTo("system_check.log", $"用户路径含中文: {result.UserPathContainsChinese}");
        _log.LogTo("system_check.log", $"程序路径含中文: {result.AppPathContainsChinese}");

        // 管理员权限
        result.IsAdmin = AdminService.IsRunAsAdmin();
        _log.LogTo("system_check.log", $"管理员权限: {result.IsAdmin}");

        _log.LogTo("system_check.log", "========== 系统兼容性检测完成 ==========");
        return result;
    }

    /// <summary>格式化检测结果为显示文本</summary>
    public string FormatResult(SystemCompatibilityResult r)
    {
        var lines = new List<string>
        {
            "━━━━━━━━━━ 系统兼容性检测结果 ━━━━━━━━━━",
            "",
            // 系统位数
            r.Is64Bit
                ? "✅ 系统位数：64 位，符合要求"
                : "❌ 系统位数：非 64 位，不支持安装 TIA Portal",
            // Windows 版本
            r.IsVersionSupported
                ? $"✅ 系统版本：{r.WindowsEdition} (Build {r.WindowsBuild})"
                : $"⚠️ 系统版本：{r.WindowsEdition} (Build {r.WindowsBuild})，可能不在官方支持列表中",
            // 管理员
            r.IsAdmin
                ? "✅ 管理员权限：正常"
                : "❌ 管理员权限：异常",
            "",
            // 硬件
            $"✅ CPU：{r.CpuName} ({r.CpuCores} 核)",
            r.MemoryGB >= 16
                ? $"✅ 内存：{r.MemoryGB:F1} GB，符合推荐要求"
                : r.MemoryGB >= 8
                    ? $"⚠️ 内存：{r.MemoryGB:F1} GB，满足最低要求（推荐 16GB）"
                    : $"❌ 内存：{r.MemoryGB:F1} GB，不满足最低 8GB 要求",
            "",
            // 磁盘
            r.SystemDriveFreeGB >= 80
                ? $"✅ 系统盘剩余：{r.SystemDriveFreeGB:F1} GB"
                : r.SystemDriveFreeGB >= 50
                    ? $"⚠️ 系统盘剩余：{r.SystemDriveFreeGB:F1} GB（建议预留 80GB 以上）"
                    : $"❌ 系统盘剩余：{r.SystemDriveFreeGB:F1} GB（建议预留 50GB 以上）",
        };

        if (r.SSDDetectable)
            lines.Add(r.IsSSD ? "✅ 磁盘类型：SSD" : "⚠️ 磁盘类型：可能为 HDD，推荐使用 SSD");

        lines.Add("");

        // 分辨率
        lines.Add(r.IsResolutionOk
            ? $"✅ 分辨率：{r.ScreenWidth}x{r.ScreenHeight}"
            : $"⚠️ 分辨率：{r.ScreenWidth}x{r.ScreenHeight}（推荐 1920x1080 或更高）");

        // 网络
        lines.Add($"ℹ️ 网络适配器：{r.NetworkAdapters}");
        lines.Add("");

        // 路径
        if (r.UserPathContainsChinese)
            lines.Add("⚠️ 当前用户名包含中文，可能影响部分安装程序");
        else
            lines.Add("✅ 用户路径：正常（无中文）");

        if (r.AppPathContainsChinese)
            lines.Add("⚠️ 当前程序路径包含中文，建议放到英文路径");
        else
            lines.Add("✅ 程序路径：正常（无中文）");

        lines.Add("");
        lines.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        return string.Join(Environment.NewLine, lines);
    }

    // ---- 私有方法 ----

    private string? GetWmiValue(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (var obj in searcher.Get())
            {
                return obj[propertyName]?.ToString();
            }
        }
        catch { }
        return null;
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

    private bool DetectSSD()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT MediaType FROM MSFT_PhysicalDisk");
            searcher.Scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            foreach (var obj in searcher.Get())
            {
                var mediaType = obj["MediaType"]?.ToString();
                // MediaType: 3 = HDD, 4 = SSD
                if (mediaType == "4") return true;
            }
        }
        catch { }
        return false;
    }

    private string GetNetworkAdapters()
    {
        var adapters = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_NetworkAdapter WHERE NetEnabled = True");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    adapters.Add(name);
            }
        }
        catch { }
        return adapters.Count > 0 ? string.Join("; ", adapters) : "未检测到";
    }

    private string GetWindowsEdition()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                return obj["Caption"]?.ToString() ?? "未知";
            }
        }
        catch { }
        return "未知";
    }

    private bool CheckVersionSupported(string build, string edition)
    {
        // Windows 10 Build 19041+ (2004/20H1 及以后) 或 Windows 11 或 Windows Server 2016+
        if (int.TryParse(build, out var buildNum))
        {
            // Win10 20H1+ = 19041+, Win11 = 22000+, Server 2016 = 14393+
            if (buildNum >= 19041) return true;
            if (buildNum >= 14393 && edition.Contains("Server", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
