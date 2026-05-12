using ZKSystemPrep.Models;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// 卸载工具启动服务
/// 重要原则：只负责启动 Geek Uninstaller.exe，不自动卸载任何软件，不自动清理注册表
/// </summary>
public class UninstallToolService
{
    private readonly LogService _log = LogService.Instance;
    private const string ToolRelativePath = "tools/Geek Uninstaller.exe";

    /// <summary>定位 Geek Uninstaller.exe</summary>
    public string? LocateGeekUninstaller()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var fullPath = Path.Combine(basePath, ToolRelativePath);

        // 也尝试反斜杠路径
        if (!File.Exists(fullPath))
            fullPath = Path.Combine(basePath, "tools", "Geek Uninstaller.exe");

        return File.Exists(fullPath) ? fullPath : null;
    }

    /// <summary>以管理员权限启动 Geek Uninstaller</summary>
    public UninstallToolResult LaunchGeekUninstallerAsAdmin()
    {
        var result = new UninstallToolResult();
        var toolPath = LocateGeekUninstaller();

        result.FilePath = toolPath ?? "";
        result.FileExists = toolPath != null;

        _log.LogTo("uninstall_tool.log", $"启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _log.LogTo("uninstall_tool.log", $"工具路径: {result.FilePath}");
        _log.LogTo("uninstall_tool.log", $"文件存在: {result.FileExists}");
        _log.LogTo("uninstall_tool.log", $"管理员启动: 是");

        if (!result.FileExists)
        {
            result.ErrorMessage = "未找到卸载工具，请确认 tools 目录下存在 Geek Uninstaller.exe。";
            _log.LogTo("uninstall_tool.log", result.ErrorMessage, LogService.LogLevel.ERROR);
            return result;
        }

        try
        {
            var workDir = Path.GetDirectoryName(toolPath!) ?? "";
            var success = ProcessHelper.LaunchAsAdmin(toolPath!, workingDirectory: workDir);

            result.LaunchSuccess = success;

            if (success)
            {
                _log.LogTo("uninstall_tool.log", "Geek Uninstaller 启动成功", LogService.LogLevel.SUCCESS);
            }
            else
            {
                result.ErrorMessage = "卸载工具启动失败，请检查文件是否存在，或右键以管理员身份运行本软件。";
                _log.LogTo("uninstall_tool.log", result.ErrorMessage, LogService.LogLevel.ERROR);
            }
        }
        catch (Exception ex)
        {
            result.LaunchSuccess = false;
            result.ErrorMessage = $"启动异常: {ex.Message}";
            _log.LogTo("uninstall_tool.log", result.ErrorMessage, LogService.LogLevel.ERROR);
        }

        return result;
    }

    /// <summary>获取启动前的安全确认文案</summary>
    public string GetLaunchConfirmation()
    {
        return "即将打开第三方卸载工具。请谨慎选择要卸载的软件，不要随意删除不确定的程序。\n\n" +
               "建议优先卸载明确属于 Siemens / TIA Portal / STEP7 / WinCC / PLCSIM / Startdrive 的组件。\n\n" +
               "是否继续？";
    }
}
