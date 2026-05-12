namespace ZKSystemPrep.Models;

/// <summary>
/// 系统兼容性检测结果
/// </summary>
public class SystemCompatibilityResult
{
    public string CpuName { get; set; } = "";
    public int CpuCores { get; set; }
    public double MemoryGB { get; set; }
    public double SystemDriveFreeGB { get; set; }
    public bool IsSSD { get; set; }
    public bool SSDDetectable { get; set; }
    public string NetworkAdapters { get; set; } = "";
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public string WindowsVersion { get; set; } = "";
    public string WindowsBuild { get; set; } = "";
    public string WindowsEdition { get; set; } = "";
    public bool Is64Bit { get; set; }
    public string UserName { get; set; } = "";
    public string UserProfilePath { get; set; } = "";
    public bool UserPathContainsChinese { get; set; }
    public bool AppPathContainsChinese { get; set; }
    public bool IsAdmin { get; set; }

    // 兼容性判断
    public bool IsSystemSupported { get; set; } = true;
    public bool IsMemorySufficient { get; set; } = true;
    public bool IsDiskSpaceSufficient { get; set; } = true;
    public bool IsResolutionOk { get; set; } = true;
    public bool IsVersionSupported { get; set; } = true;
}
