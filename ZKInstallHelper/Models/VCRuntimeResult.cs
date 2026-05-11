namespace ZKInstallHelper.Models;

/// <summary>
/// VC++ 运行库检测结果（单架构）
/// </summary>
public class VCRuntimeResult
{
    public string Architecture { get; set; } = ""; // x64 / x86
    public bool Installed { get; set; }
    public string Version { get; set; } = "";
    public int Build { get; set; }
    public int Major { get; set; }
    public int Minor { get; set; }

    /// <summary>从卸载注册表扫描到的条目</summary>
    public List<string> UninstallEntries { get; set; } = new();

    public string StatusText => Installed
        ? $"✅ VC++ {Architecture} 已安装 (版本: {Version}, Build: {Build})"
        : $"❌ VC++ {Architecture} 未安装或异常";
}
