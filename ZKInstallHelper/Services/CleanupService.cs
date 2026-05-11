using ZKInstallHelper.Utils;

namespace ZKInstallHelper.Services;

/// <summary>
/// 安装缓存清理服务（安全清理，不删除系统关键目录）
/// </summary>
public class CleanupService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>执行安全清理</summary>
    public (int deleted, int skipped, long freedBytes) CleanTempFiles()
    {
        _log.LogTo("install_action.log", "开始清理安装缓存...");
        _log.Log("开始清理临时文件...");

        int totalDeleted = 0, totalSkipped = 0;
        long totalFreed = 0;

        // 清理用户 TEMP
        var userTemp = Path.GetTempPath();
        _log.Log($"清理用户临时目录: {userTemp}");
        var (d1, s1, f1) = FileHelper.SafeCleanDirectory(userTemp);
        totalDeleted += d1;
        totalSkipped += s1;
        totalFreed += f1;

        // 清理 Windows\Temp
        var winTemp = @"C:\Windows\Temp";
        if (Directory.Exists(winTemp))
        {
            _log.Log($"清理系统临时目录: {winTemp}");
            var (d2, s2, f2) = FileHelper.SafeCleanDirectory(winTemp);
            totalDeleted += d2;
            totalSkipped += s2;
            totalFreed += f2;
        }

        var summary = $"清理完成：已删除 {totalDeleted} 个文件，跳过 {totalSkipped} 个文件，释放 {FileHelper.FormatBytes(totalFreed)}";
        _log.Log(summary, LogService.LogLevel.SUCCESS);
        _log.LogTo("install_action.log", summary);

        return (totalDeleted, totalSkipped, totalFreed);
    }
}
