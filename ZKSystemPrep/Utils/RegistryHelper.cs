using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ZKSystemPrep.Utils;

/// <summary>
/// 注册表操作辅助工具
/// </summary>
public static class RegistryHelper
{
    /// <summary>读取 HKLM 下的注册表值</summary>
    public static object? GetLocalMachineValue(string subKey, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey);
            return key?.GetValue(valueName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>检查 HKLM 子键是否存在</summary>
    public static bool SubKeyExists(string subKey)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>检查指定注册表值是否存在</summary>
    public static bool ValueExists(string subKey, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey);
            if (key == null) return false;
            return key.GetValue(valueName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>删除 HKLM 下的单个注册表值（不删除整个键）</summary>
    public static bool DeleteValue(string subKey, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey, writable: true);
            if (key == null) return false;
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>使用 reg export 导出注册表键到 .reg 文件</summary>
    public static bool ExportKey(string fullKeyPath, string exportFilePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(exportFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var startInfo = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export \"{fullKeyPath}\" \"{exportFilePath}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;
            process.WaitForExit(30000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
