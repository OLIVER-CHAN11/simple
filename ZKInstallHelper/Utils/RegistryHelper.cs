using System;
using Microsoft.Win32;

namespace ZKInstallHelper.Utils;

/// <summary>
/// 注册表读取辅助工具
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

    /// <summary>枚举卸载注册表中匹配关键词的项目</summary>
    public static List<string> FindUninstallEntries(params string[] keywords)
    {
        var results = new List<string>();
        string[] uninstallPaths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var path in uninstallPaths)
        {
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(path);
                if (baseKey == null) continue;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrEmpty(displayName)) continue;

                        foreach (var kw in keywords)
                        {
                            if (displayName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                            {
                                results.Add(displayName);
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        return results;
    }
}
