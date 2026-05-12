namespace ZKSystemPrep.Models;

/// <summary>
/// 检测到的杀毒软件进程信息
/// </summary>
public class AntivirusProcessItem
{
    /// <summary>杀毒软件品牌名称</summary>
    public string BrandName { get; set; } = "";

    /// <summary>检测到的进程名</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>进程 PID（仅记录，不用于 kill）</summary>
    public int Pid { get; set; }
}
