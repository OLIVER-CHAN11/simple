using System;
using System.Diagnostics;

namespace ZKInstallHelper.Utils;

/// <summary>
/// 进程操作工具
/// </summary>
public static class ProcessHelper
{
    /// <summary>以管理员权限启动进程并等待退出</summary>
    public static int RunAndWait(string fileName, string arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? ""
        };

        using var process = Process.Start(startInfo);
        if (process == null) return -1;
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>启动进程不等待</summary>
    public static void RunNoWait(string fileName, string? arguments = null, string? workingDirectory = null)
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
    }

    /// <summary>用默认浏览器打开 URL</summary>
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    /// <summary>用资源管理器打开目录</summary>
    public static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
    }

    /// <summary>检查是否有指定进程在运行</summary>
    public static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>检测常见杀毒软件</summary>
    public static List<string> DetectAntivirusProcesses()
    {
        var found = new List<string>();
        var map = new Dictionary<string, string>
        {
            { "360sd", "360杀毒" },
            { "360safe", "360安全卫士" },
            { "360tray", "360安全卫士" },
            { "ZhuDongFangYu", "360主动防御" },
            { "QQPCTray", "腾讯电脑管家" },
            { "qqpctray", "腾讯电脑管家" },
            { "HipsTray", "火绒安全" },
            { "usysdiag", "火绒安全" },
            { "wsctrl", "火绒安全" },
            { "kxetray", "金山毒霸" },
            { "kwsprotect64", "金山毒霸" },
        };

        foreach (var (proc, name) in map)
        {
            if (IsProcessRunning(proc) && !found.Contains(name))
                found.Add(name);
        }
        return found;
    }
}
