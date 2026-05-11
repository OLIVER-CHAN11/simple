using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ZKInstallHelper.Utils;

/// <summary>
/// 文件和路径工具
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

    /// <summary>安全删除文件（跳过占用）</summary>
    public static (int deleted, int skipped, long freedBytes) SafeCleanDirectory(string dirPath)
    {
        int deleted = 0, skipped = 0;
        long freedBytes = 0;

        if (!Directory.Exists(dirPath))
            return (deleted, skipped, freedBytes);

        try
        {
            foreach (var file in Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    var size = info.Length;
                    info.Delete();
                    deleted++;
                    freedBytes += size;
                }
                catch
                {
                    skipped++;
                }
            }
        }
        catch { }

        return (deleted, skipped, freedBytes);
    }

    /// <summary>递归查找安装入口文件</summary>
    public static string? FindInstallerEntry(string directory)
    {
        string[] priorities = { "Start.exe", "Setup.exe", "setup.exe", "Install.exe", "installer.exe" };

        foreach (var name in priorities)
        {
            try
            {
                var files = Directory.GetFiles(directory, name, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return files[0];
            }
            catch { }
        }
        return null;
    }

    /// <summary>格式化字节为易读格式</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
