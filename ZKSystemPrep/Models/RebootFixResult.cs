namespace ZKSystemPrep.Models;

/// <summary>
/// Siemens 重启提示修复结果
/// </summary>
public class RebootFixResult
{
    /// <summary>是否检测到 PendingFileRenameOperations</summary>
    public bool PendingFound { get; set; }

    /// <summary>注册表值内容（用于日志记录）</summary>
    public string PendingValue { get; set; } = "";

    /// <summary>是否已备份</summary>
    public bool BackupCreated { get; set; }

    /// <summary>备份文件路径</summary>
    public string BackupPath { get; set; } = "";

    /// <summary>是否已删除</summary>
    public bool Deleted { get; set; }

    /// <summary>操作摘要</summary>
    public string Summary { get; set; } = "";
}
