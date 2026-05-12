using ZKSystemPrep.Models;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// Siemens 安装重启提示修复服务
/// 安全流程：检测 → 备份 → 二次确认 → 删除值（不删除整个键）
/// </summary>
public class SiemensRebootFixService
{
    private readonly LogService _log = LogService.Instance;

    // 注册表路径和值名
    private const string RegPath1 = @"SYSTEM\CurrentControlSet\Control\Session Manager";
    private const string RegPath2 = @"SYSTEM\ControlSet001\Control\Session Manager";
    private const string ValueName = "PendingFileRenameOperations";
    private const string FullKeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager";

    /// <summary>检测 PendingFileRenameOperations 是否存在</summary>
    public RebootFixResult Detect()
    {
        _log.LogTo("reboot_fix.log", "检测 PendingFileRenameOperations...");
        var result = new RebootFixResult();

        var exists1 = RegistryHelper.ValueExists(RegPath1, ValueName);
        var exists2 = RegistryHelper.ValueExists(RegPath2, ValueName);

        result.PendingFound = exists1 || exists2;

        if (result.PendingFound)
        {
            // 读取值内容用于日志记录
            var value = RegistryHelper.GetLocalMachineValue(RegPath1, ValueName);
            if (value is string[] arr)
                result.PendingValue = $"[数组, {arr.Length} 项]";
            else if (value != null)
                result.PendingValue = value.ToString()?.Substring(0, Math.Min(200, value.ToString()!.Length)) ?? "";

            result.Summary = "检测到系统存在 PendingFileRenameOperations（待重启状态）。";
            _log.LogTo("reboot_fix.log", result.Summary, LogService.LogLevel.WARN);
            _log.LogTo("reboot_fix.log", $"值内容预览: {result.PendingValue}");
        }
        else
        {
            result.Summary = "未检测到 PendingFileRenameOperations，当前没有发现该类重启挂起标记。";
            _log.LogTo("reboot_fix.log", result.Summary);
        }

        return result;
    }

    /// <summary>备份注册表键</summary>
    public bool Backup(RebootFixResult result)
    {
        var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "registry");
        FileHelper.EnsureDirectory(backupDir);

        var fileName = $"SessionManager_{DateTime.Now:yyyyMMdd_HHmmss}.reg";
        var backupPath = Path.Combine(backupDir, fileName);

        _log.LogTo("reboot_fix.log", $"正在备份注册表到: {backupPath}");

        var success = RegistryHelper.ExportKey(FullKeyPath, backupPath);

        if (success)
        {
            result.BackupCreated = true;
            result.BackupPath = backupPath;
            _log.LogTo("reboot_fix.log", "注册表备份成功", LogService.LogLevel.SUCCESS);
        }
        else
        {
            _log.LogTo("reboot_fix.log", "注册表备份失败", LogService.LogLevel.ERROR);
        }

        return success;
    }

    /// <summary>删除 PendingFileRenameOperations 值（不删除整个键）</summary>
    public bool DeletePendingValue(RebootFixResult result)
    {
        _log.LogTo("reboot_fix.log", "正在删除 PendingFileRenameOperations...");

        // 删除 CurrentControlSet 下的
        var success1 = RegistryHelper.DeleteValue(RegPath1, ValueName);
        // 也尝试删除 ControlSet001 下的
        var success2 = RegistryHelper.DeleteValue(RegPath2, ValueName);

        result.Deleted = success1 || success2;

        if (result.Deleted)
        {
            result.Summary = "已处理 Siemens 安装反复提示重启相关注册表值。建议重新运行安装程序。如后续系统异常，可使用备份 reg 文件恢复。";
            _log.LogTo("reboot_fix.log", "PendingFileRenameOperations 已删除", LogService.LogLevel.SUCCESS);
        }
        else
        {
            result.Summary = "删除 PendingFileRenameOperations 失败，请检查权限或手动处理。";
            _log.LogTo("reboot_fix.log", result.Summary, LogService.LogLevel.ERROR);
        }

        return result.Deleted;
    }

    /// <summary>获取检测结果的显示文本</summary>
    public string FormatDetectResult(RebootFixResult result)
    {
        if (!result.PendingFound)
        {
            return
                "━━━━━━━━━━ Siemens 重启提示检测 ━━━━━━━━━━\n\n" +
                "✅ 未检测到 PendingFileRenameOperations。\n" +
                "当前没有发现该类重启挂起标记。\n\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }

        return
            "━━━━━━━━━━ Siemens 重启提示检测 ━━━━━━━━━━\n\n" +
            "⚠️ 检测到系统存在待重启状态。\n\n" +
            "该状态可能导致 Siemens / TIA Portal 安装时反复提示重启。\n\n" +
            "建议：\n" +
            "1. 优先正常重启电脑\n" +
            "2. 如果重启后仍然存在，可备份后删除该注册表值\n\n" +
            "点击"继续处理"将备份注册表并删除该值。\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }

    /// <summary>获取二次确认的警告文案</summary>
    public string GetConfirmationWarning()
    {
        return "即将备份并删除 PendingFileRenameOperations 注册表值。\n\n" +
               "该操作可能影响正在等待重启完成的其他软件安装。\n\n" +
               "是否确认继续？";
    }
}
