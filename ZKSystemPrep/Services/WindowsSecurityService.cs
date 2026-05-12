using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// Windows 安全中心设置页面打开服务
/// 重要原则：不直接修改 Smart App Control 状态，不关闭 Defender，只打开设置页面
/// </summary>
public class WindowsSecurityService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>尝试打开智能应用控制设置页面</summary>
    public bool OpenSmartAppControlSettings()
    {
        _log.LogTo("windows_security.log", "尝试打开智能应用控制设置页面...");

        // 优先尝试 appbrowser URI
        if (ProcessHelper.OpenSettingsUri("windowsdefender://appbrowser"))
        {
            _log.LogTo("windows_security.log", "已通过 windowsdefender://appbrowser 打开设置页面", LogService.LogLevel.SUCCESS);
            return true;
        }

        // 备用：打开 Windows 安全中心主页
        _log.LogTo("windows_security.log", "appbrowser URI 无效，尝试打开 Windows 安全中心主页...");
        if (ProcessHelper.OpenSettingsUri("windowsdefender:"))
        {
            _log.LogTo("windows_security.log", "已通过 windowsdefender: 打开 Windows 安全中心", LogService.LogLevel.SUCCESS);
            return true;
        }

        // 最后兜底：通过 ms-settings
        _log.LogTo("windows_security.log", "尝试通过 ms-settings 打开...");
        if (ProcessHelper.OpenSettingsUri("ms-settings:windowsdefender"))
        {
            _log.LogTo("windows_security.log", "已通过 ms-settings:windowsdefender 打开", LogService.LogLevel.SUCCESS);
            return true;
        }

        _log.LogTo("windows_security.log", "无法打开 Windows 安全中心设置页面", LogService.LogLevel.ERROR);
        return false;
    }

    /// <summary>获取操作提示文案</summary>
    public string GetInstructions()
    {
        return
            "━━━━━━━━━━ 智能应用控制设置 ━━━━━━━━━━\n\n" +
            "已尝试打开 Windows 安全中心设置页面。\n\n" +
            "请手动进入：\n" +
            "  Windows 安全中心\n" +
            "  → 应用和浏览器控制\n" +
            "  → 智能应用控制\n" +
            "  → 根据需要查看当前状态\n\n" +
            "⚠️ 本工具只负责打开设置页面。\n" +
            "请用户根据自己的需求手动查看或调整智能应用控制状态。\n" +
            "安装完成后建议恢复安全设置。\n\n" +
            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    }
}
