using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZKInstallHelper.Models;

/// <summary>
/// 应用配置（对应 config/manifest.json）
/// </summary>
public class AppConfig
{
    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "1.0.0";

    [JsonPropertyName("remoteSupportUrl")]
    public string RemoteSupportUrl { get; set; } = "";

    [JsonPropertyName("wechatText")]
    public string WechatText { get; set; } = "";

    [JsonPropertyName("contactPhone")]
    public string ContactPhone { get; set; } = "";

    [JsonPropertyName("files")]
    public List<DownloadFile> Files { get; set; } = new();

    /// <summary>从 manifest.json 加载配置，失败则返回默认值</summary>
    public static AppConfig Load()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "manifest.json");
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null) return config;
            }
            catch { }
        }
        return GetDefault();
    }

    /// <summary>默认配置（代码内置，不依赖文件）</summary>
    public static AppConfig GetDefault()
    {
        return new AppConfig
        {
            AppVersion = "1.0.0",
            RemoteSupportUrl = "",
            Files = new List<DownloadFile>
            {
                new()
                {
                    Name = "vc_redist.x64.exe",
                    Url = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                    Type = "runtime",
                    Arch = "x64"
                },
                new()
                {
                    Name = "vc_redist.x86.exe",
                    Url = "https://aka.ms/vs/17/release/vc_redist.x86.exe",
                    Type = "runtime",
                    Arch = "x86"
                }
            }
        };
    }
}

public class DownloadFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("arch")]
    public string Arch { get; set; } = "";
}
