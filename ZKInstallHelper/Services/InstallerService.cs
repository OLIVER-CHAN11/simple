using ZKInstallHelper.Utils;

namespace ZKInstallHelper.Services;

/// <summary>
/// 安装包启动服务
/// </summary>
public class InstallerService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>当前选择的安装包目录</summary>
    public string? SelectedDirectory { get; set; }

    /// <summary>检测到的安装入口</summary>
    public string? DetectedEntry { get; set; }

    /// <summary>选择安装包目录并查找入口</summary>
    public string? SelectAndDetect(string directory)
    {
        SelectedDirectory = directory;
        _log.LogTo("install_action.log", $"选择安装包目录: {directory}");

        DetectedEntry = FileHelper.FindInstallerEntry(directory);

        if (DetectedEntry != null)
        {
            _log.LogTo("install_action.log", $"检测到安装程序: {DetectedEntry}");
            _log.Log($"检测到安装程序：{DetectedEntry}", LogService.LogLevel.SUCCESS);
        }
        else
        {
            _log.LogTo("install_action.log", "未找到安装入口");
            _log.Log("未找到安装入口，请确认选择的是完整安装包目录。", LogService.LogLevel.WARN);
        }

        return DetectedEntry;
    }

    /// <summary>启动安装程序</summary>
    public bool StartInstall()
    {
        if (string.IsNullOrEmpty(DetectedEntry) || !File.Exists(DetectedEntry))
        {
            _log.Log("请先选择安装包目录并确认安装入口。", LogService.LogLevel.WARN);
            return false;
        }

        _log.LogTo("install_action.log", $"启动安装程序: {DetectedEntry}");
        _log.Log($"正在启动安装程序：{Path.GetFileName(DetectedEntry)}");

        try
        {
            var workDir = Path.GetDirectoryName(DetectedEntry) ?? "";
            ProcessHelper.RunNoWait(DetectedEntry, null, workDir);
            _log.LogTo("install_action.log", "安装程序已启动");
            _log.Log("安装程序已启动，请在安装向导中继续操作。", LogService.LogLevel.SUCCESS);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log($"启动安装程序失败: {ex.Message}", LogService.LogLevel.ERROR);
            _log.LogTo("install_action.log", $"启动失败: {ex.Message}");
            return false;
        }
    }
}
