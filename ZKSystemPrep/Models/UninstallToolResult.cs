namespace ZKSystemPrep.Models;

/// <summary>
/// 卸载工具启动结果
/// </summary>
public class UninstallToolResult
{
    /// <summary>工具 exe 是否存在</summary>
    public bool FileExists { get; set; }

    /// <summary>工具完整路径</summary>
    public string FilePath { get; set; } = "";

    /// <summary>是否启动成功</summary>
    public bool LaunchSuccess { get; set; }

    /// <summary>错误信息（如有）</summary>
    public string ErrorMessage { get; set; } = "";
}
