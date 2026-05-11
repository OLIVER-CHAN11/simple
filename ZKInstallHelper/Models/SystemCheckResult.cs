namespace ZKInstallHelper.Models;

/// <summary>
/// 系统环境检测结果
/// </summary>
public class SystemCheckResult
{
    public string WindowsVersion { get; set; } = "";
    public string WindowsBuild { get; set; } = "";
    public bool Is64Bit { get; set; }
    public string CpuInfo { get; set; } = "";
    public double MemoryGB { get; set; }
    public double CDriveFreeGB { get; set; }
    public double DDriveFreeGB { get; set; } = -1; // -1 表示不存在
    public string UserName { get; set; } = "";
    public string UserProfilePath { get; set; } = "";
    public bool UserPathContainsChinese { get; set; }
    public bool AppPathContainsChinese { get; set; }
    public string TempPath { get; set; } = "";
    public bool IsAdmin { get; set; }

    // VC++ 运行库状态
    public VCRuntimeResult VCx64 { get; set; } = new();
    public VCRuntimeResult VCx86 { get; set; } = new();

    // Siemens 相关
    public bool SiemensLogDirExists { get; set; }
    public string SiemensLogDir { get; set; } = @"C:\ProgramData\Siemens\Automation\Logfiles\Setup";

    // 杀毒软件
    public List<string> DetectedAntivirus { get; set; } = new();
}
