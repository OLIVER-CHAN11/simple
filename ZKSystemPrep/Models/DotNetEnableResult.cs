namespace ZKSystemPrep.Models;

/// <summary>
/// .NET Framework 3.5 启用结果
/// </summary>
public class DotNetEnableResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
    public string ErrorOutput { get; set; } = "";
    public string Summary { get; set; } = "";
}
