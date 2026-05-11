using System;
using System.Net.Http;

namespace ZKInstallHelper.Services;

/// <summary>
/// 文件下载服务（带进度回调）
/// </summary>
public class DownloadService
{
    private readonly LogService _log = LogService.Instance;
    private readonly HttpClient _client;

    public DownloadService()
    {
        _client = new HttpClient();
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// 下载文件到指定路径
    /// </summary>
    /// <param name="url">下载地址</param>
    /// <param name="destPath">本地保存路径</param>
    /// <param name="progress">进度回调 (0-100)</param>
    /// <returns>是否成功</returns>
    public async Task<bool> DownloadFileAsync(string url, string destPath, Action<int>? progress = null)
    {
        _log.Log($"开始下载: {url}");
        _log.Log($"保存到: {destPath}");

        try
        {
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            _log.Log($"文件大小: {(totalBytes > 0 ? $"{totalBytes / (1024.0 * 1024):F1} MB" : "未知")}");

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            int lastPercent = 0;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var percent = (int)(totalRead * 100 / totalBytes);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Invoke(percent);
                    }
                }
            }

            progress?.Invoke(100);
            _log.Log($"下载完成: {Path.GetFileName(destPath)}", LogService.LogLevel.SUCCESS);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log($"下载失败: {ex.Message}", LogService.LogLevel.ERROR);
            _log.Log("请检查网络连接，或尝试使用手机热点/VPN。", LogService.LogLevel.WARN);
            return false;
        }
    }
}
