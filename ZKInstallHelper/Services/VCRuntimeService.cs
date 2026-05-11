using ZKInstallHelper.Models;
using ZKInstallHelper.Utils;

namespace ZKInstallHelper.Services;

/// <summary>
/// VC++ 运行库检测与安装服务
/// </summary>
public class VCRuntimeService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>检测指定架构的 VC++ 运行库状态</summary>
    public VCRuntimeResult CheckRuntime(string arch)
    {
        var result = new VCRuntimeResult { Architecture = arch };

        // 检测注册表主键
        var regPath = $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{arch}";
        var installed = RegistryHelper.GetLocalMachineValue(regPath, "Installed");
        var version = RegistryHelper.GetLocalMachineValue(regPath, "Version")?.ToString() ?? "";
        var bld = RegistryHelper.GetLocalMachineValue(regPath, "Bld");
        var major = RegistryHelper.GetLocalMachineValue(regPath, "Major");
        var minor = RegistryHelper.GetLocalMachineValue(regPath, "Minor");

        if (installed != null && Convert.ToInt32(installed) == 1)
        {
            result.Installed = true;
            result.Version = version;
            result.Build = bld != null ? Convert.ToInt32(bld) : 0;
            result.Major = major != null ? Convert.ToInt32(major) : 0;
            result.Minor = minor != null ? Convert.ToInt32(minor) : 0;
        }

        // 扫描卸载注册表补充信息
        result.UninstallEntries = RegistryHelper.FindUninstallEntries(
            "Microsoft Visual C++ 2015-2022",
            "Microsoft Visual C++ 2022",
            "Minimum Runtime",
            "Additional Runtime"
        );

        // 按架构过滤
        if (arch == "x64")
            result.UninstallEntries = result.UninstallEntries
                .Where(e => e.Contains("x64", StringComparison.OrdinalIgnoreCase) ||
                           e.Contains("X64", StringComparison.OrdinalIgnoreCase))
                .ToList();
        else
            result.UninstallEntries = result.UninstallEntries
                .Where(e => e.Contains("x86", StringComparison.OrdinalIgnoreCase) ||
                           e.Contains("X86", StringComparison.OrdinalIgnoreCase) ||
                           (!e.Contains("x64", StringComparison.OrdinalIgnoreCase) &&
                            !e.Contains("X64", StringComparison.OrdinalIgnoreCase)))
                .ToList();

        return result;
    }

    /// <summary>安装 VC++ 运行库（静默模式）</summary>
    /// <returns>退出码</returns>
    public int Install(string exePath, string arch)
    {
        var logFileName = $"vc_install_{arch}.log";
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", logFileName);
        var args = $"/install /quiet /norestart /log \"{logPath}\"";

        _log.Log($"开始安装 VC++ {arch}：{exePath}");
        _log.Log($"安装参数：{args}");

        var exitCode = ProcessHelper.RunAndWait(exePath, args);

        switch (exitCode)
        {
            case 0:
                _log.Log($"VC++ {arch} 安装成功（ExitCode: 0）", LogService.LogLevel.SUCCESS);
                break;
            case 3010:
                _log.Log($"VC++ {arch} 安装成功，需要重启系统（ExitCode: 3010）", LogService.LogLevel.WARN);
                break;
            default:
                _log.Log($"VC++ {arch} 安装失败（ExitCode: {exitCode}）", LogService.LogLevel.ERROR);
                AnalyzeFailure(logPath, arch);
                break;
        }

        return exitCode;
    }

    /// <summary>分析安装失败日志</summary>
    private void AnalyzeFailure(string logPath, string arch)
    {
        if (!File.Exists(logPath)) return;

        try
        {
            var content = File.ReadAllText(logPath);
            var errorKeywords = new[]
            {
                "vc_runtimeMinimum_x64.msi",
                "Minimum Runtime",
                "Another version",
                "missing source",
                "vc_runtimeMinimum"
            };

            foreach (var kw in errorKeywords)
            {
                if (content.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Log(
                        $"检测到 VC++ 安装缓存或卸载残留异常（关键词: {kw}），" +
                        "建议使用微软官方卸载修复工具清理 Microsoft Visual C++ 2022 X64 Minimum Runtime 后再重试。",
                        LogService.LogLevel.ERROR);
                    return;
                }
            }
        }
        catch { }
    }
}
