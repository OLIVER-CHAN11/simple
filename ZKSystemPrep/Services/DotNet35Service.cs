using ZKSystemPrep.Models;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// .NET Framework 3.5 启用服务
/// </summary>
public class DotNet35Service
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>使用 DISM 启用 .NET Framework 3.5，实时回调输出</summary>
    public DotNetEnableResult Enable(Action<string>? onOutput = null)
    {
        _log.LogTo("dotnet35.log", "========== 开始启用 .NET Framework 3.5 ==========");
        var result = new DotNetEnableResult();

        var allOutput = new List<string>();
        var allErrors = new List<string>();

        var exitCode = ProcessHelper.RunWithLiveOutput(
            "dism.exe",
            "/online /enable-feature /featurename:NetFx3 /All /NoRestart",
            onOutput: line =>
            {
                allOutput.Add(line);
                onOutput?.Invoke(line);
                _log.LogToFileOnly("dotnet35.log", line);
            },
            onError: line =>
            {
                allErrors.Add(line);
                onOutput?.Invoke($"[ERROR] {line}");
                _log.LogToFileOnly("dotnet35.log", $"[ERROR] {line}");
            },
            timeoutMs: 600000 // DISM 可能较慢，给 10 分钟
        );

        result.ExitCode = exitCode;
        result.Output = string.Join(Environment.NewLine, allOutput);
        result.ErrorOutput = string.Join(Environment.NewLine, allErrors);
        result.Success = exitCode == 0;

        if (result.Success)
        {
            result.Summary = ".NET Framework 3.5 启用成功。";
            _log.LogTo("dotnet35.log", result.Summary, LogService.LogLevel.SUCCESS);
        }
        else
        {
            result.Summary = $".NET Framework 3.5 启用失败（ExitCode: {exitCode}）。";
            _log.LogTo("dotnet35.log", result.Summary, LogService.LogLevel.ERROR);
        }

        _log.LogTo("dotnet35.log", "========== .NET Framework 3.5 操作完成 ==========");
        return result;
    }

    /// <summary>获取失败时的建议文案</summary>
    public string GetFailureSuggestions(int exitCode)
    {
        return
            $"━━━━━━━━━━ .NET Framework 3.5 启用失败 ━━━━━━━━━━\n\n" +
            $"DISM 退出码：{exitCode}\n\n" +
            "建议尝试以下方案：\n\n" +
            "1. 检查网络连接是否正常（DISM 可能需要从 Windows Update 下载组件）\n\n" +
            "2. 使用 Windows 安装镜像作为源：\n" +
            "   dism /online /enable-feature /featurename:NetFx3 /All /Source:D:\\sources\\sxs\n\n" +
            "3. 手动启用：\n" +
            "   控制面板 → 程序和功能 → 启用或关闭 Windows 功能\n" +
            "   → 勾选 .NET Framework 3.5\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }
}
