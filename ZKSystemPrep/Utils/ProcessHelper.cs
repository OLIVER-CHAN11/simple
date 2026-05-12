using System;
using System.Diagnostics;

namespace ZKSystemPrep.Utils;

/// <summary>
/// 进程操作辅助工具
/// </summary>
public static class ProcessHelper
{
    /// <summary>启动进程并等待退出，捕获标准输出和错误输出</summary>
    public static (int exitCode, string output, string error) RunAndCapture(
        string fileName, string arguments, int timeoutMs = 300000)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return (-1, "", "无法启动进程");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(timeoutMs);

            return (process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    /// <summary>启动进程并实时回调输出（用于 DISM 等长时间操作）</summary>
    public static int RunWithLiveOutput(
        string fileName, string arguments,
        Action<string>? onOutput = null,
        Action<string>? onError = null,
        int timeoutMs = 600000)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return -1;

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    onOutput?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    onError?.Invoke(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit(timeoutMs);

            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>以管理员权限启动进程（不等待）</summary>
    public static bool LaunchAsAdmin(string fileName, string? arguments = null, string? workingDirectory = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? ""
            };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>用默认浏览器打开 URL</summary>
    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>用资源管理器打开文件夹</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = path, UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>启动 Windows 程序（如 taskmgr.exe, OptionalFeatures.exe）</summary>
    public static void LaunchSystemApp(string exeName)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exeName, UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>打开 Windows 设置 URI</summary>
    public static bool OpenSettingsUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>检查指定进程是否正在运行</summary>
    public static bool IsProcessRunning(string processName)
    {
        try
        {
            // 去掉 .exe 后缀
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>获取匹配进程的 PID 列表</summary>
    public static int[] GetProcessIds(string processName)
    {
        try
        {
            var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4]
                : processName;
            return Process.GetProcessesByName(name).Select(p => p.Id).ToArray();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }
}
