using System;
using System.Text.RegularExpressions;

namespace ZKSystemPrep.Utils;

/// <summary>
/// 文件和路径辅助工具
/// </summary>
public static class FileHelper
{
    /// <summary>检查路径是否包含中文字符</summary>
    public static bool ContainsChinese(string path)
    {
        return Regex.IsMatch(path, @"[\u4e00-\u9fa5]");
    }

    /// <summary>获取磁盘剩余空间（GB）</summary>
    public static double GetDriveFreeSpaceGB(string driveLetter)
    {
        try
        {
            var drive = new DriveInfo(driveLetter);
            if (drive.IsReady)
                return drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        }
        catch { }
        return -1;
    }

    /// <summary>获取磁盘总容量（GB）</summary>
    public static double GetDriveTotalSizeGB(string driveLetter)
    {
        try
        {
            var drive = new DriveInfo(driveLetter);
            if (drive.IsReady)
                return drive.TotalSize / (1024.0 * 1024 * 1024);
        }
        catch { }
        return -1;
    }

    /// <summary>格式化字节数为易读文本</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    /// <summary>获取系统盘盘符（通常是 C）</summary>
    public static string GetSystemDriveLetter()
    {
        var sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return sysRoot.Length > 0 ? sysRoot[..1] : "C";
    }

    /// <summary>确保目录存在</summary>
    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
