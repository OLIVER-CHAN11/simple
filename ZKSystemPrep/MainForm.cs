using System;
using System.Drawing;
using System.Windows.Forms;
using ZKSystemPrep.Models;
using ZKSystemPrep.Services;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep;

/// <summary>
/// 主窗体：左侧 7 个功能按钮 + 右侧日志/结果显示区
/// </summary>
public class MainForm : Form
{
    // ---- 服务实例 ----
    private readonly LogService _log = LogService.Instance;
    private readonly SystemCompatibilityService _sysCheck = new();
    private readonly AntivirusDetectionService _avDetect = new();
    private readonly WindowsSecurityService _winSecurity = new();
    private readonly DotNet35Service _dotnet35 = new();
    private readonly SiemensRebootFixService _rebootFix = new();
    private readonly UninstallToolService _uninstallTool = new();
    private readonly ReportService _report = new();

    // ---- 状态缓存（用于报告导出） ----
    private SystemCompatibilityResult? _lastSysResult;
    private List<AntivirusProcessItem>? _lastAvItems;
    private DotNetEnableResult? _lastDotnetResult;
    private RebootFixResult? _lastRebootResult;
    private UninstallToolResult? _lastUninstallResult;
    private bool _winSecurityOpened;

    // ---- UI 控件 ----
    private Panel _leftPanel = null!;
    private RichTextBox _logBox = null!;
    private Label _statusLabel = null!;

    public MainForm()
    {
        InitializeUI();
        RegisterLogCallback();
        _log.Log("智控安装助手 - 系统准备工具已启动（管理员模式）", LogService.LogLevel.SUCCESS);
    }

    // ================================================================
    // UI 初始化
    // ================================================================

    private void InitializeUI()
    {
        Text = "智控安装助手 - 系统准备工具";
        Size = new Size(920, 620);
        MinimumSize = new Size(780, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9f);

        // 左侧按钮面板
        _leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 210,
            BackColor = Color.FromArgb(245, 245, 248),
            Padding = new Padding(10, 10, 10, 10)
        };
        Controls.Add(_leftPanel);

        // 左侧标题
        var titleLabel = new Label
        {
            Text = "🛠️ 系统准备工具",
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 60),
            Dock = DockStyle.Top,
            Height = 38,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _leftPanel.Controls.Add(titleLabel);

        // 按钮容器
        string[] buttonTexts =
        {
            "检查系统兼容性",
            "检测杀毒软件",
            "打开智能应用控制设置",
            "启用 .NET Framework 3.5",
            "解除 Siemens 重启提示",
            "打开卸载工具",
            "导出系统准备报告"
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        _leftPanel.Controls.Add(buttonsPanel);
        buttonsPanel.BringToFront();

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            var btn = new Button
            {
                Text = buttonTexts[i],
                Width = 185,
                Height = 42,
                Margin = new Padding(0, 4, 0, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(50, 50, 60),
                Cursor = Cursors.Hand,
                Tag = i
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 210);
            btn.Click += OnButtonClick;
            buttonsPanel.Controls.Add(btn);
        }

        // 右侧区域
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        Controls.Add(rightPanel);
        rightPanel.BringToFront();

        // 状态标签
        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "就绪",
            ForeColor = Color.FromArgb(80, 80, 90)
        };
        rightPanel.Controls.Add(_statusLabel);

        // 日志输出框
        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(252, 252, 254),
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        rightPanel.Controls.Add(_logBox);
        _logBox.BringToFront();
    }

    private void RegisterLogCallback()
    {
        _log.OnLog += (message, level) =>
        {
            if (InvokeRequired)
                BeginInvoke(() => AppendLog(message, level));
            else
                AppendLog(message, level);
        };
    }

    private void AppendLog(string message, LogService.LogLevel level)
    {
        var color = level switch
        {
            LogService.LogLevel.ERROR => Color.Red,
            LogService.LogLevel.WARN => Color.DarkOrange,
            LogService.LogLevel.SUCCESS => Color.Green,
            _ => Color.FromArgb(60, 60, 70)
        };

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionLength = 0;
        _logBox.SelectionColor = color;
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _logBox.ScrollToCaret();
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
            BeginInvoke(() => _statusLabel.Text = text);
        else
            _statusLabel.Text = text;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        foreach (Control c in _leftPanel.Controls)
        {
            if (c is FlowLayoutPanel flow)
            {
                foreach (Control btn in flow.Controls)
                    if (btn is Button b) b.Enabled = enabled;
            }
        }
    }

    // ================================================================
    // 按钮事件路由
    // ================================================================

    private async void OnButtonClick(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int index) return;

        try
        {
            switch (index)
            {
                case 0: await DoSystemCheck(); break;
                case 1: DoAntivirusDetect(); break;
                case 2: DoOpenSmartAppControl(); break;
                case 3: await DoDotNet35Enable(); break;
                case 4: DoSiemensRebootFix(); break;
                case 5: DoOpenUninstallTool(); break;
                case 6: DoExportReport(); break;
            }
        }
        catch (Exception ex)
        {
            _log.Log($"操作异常: {ex.Message}", LogService.LogLevel.ERROR);
            MessageBox.Show($"操作过程中发生错误:\n{ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ================================================================
    // 功能实现
    // ================================================================

    /// <summary>1. 检查系统兼容性</summary>
    private async Task DoSystemCheck()
    {
        SetButtonsEnabled(false);
        SetStatus("正在检测系统兼容性...");
        _logBox.Clear();

        await Task.Run(() =>
        {
            _lastSysResult = _sysCheck.RunCheck();
        });

        var text = _sysCheck.FormatResult(_lastSysResult!);
        AppendLog(text, LogService.LogLevel.INFO);

        SetStatus("系统兼容性检测完成");
        SetButtonsEnabled(true);
    }

    /// <summary>2. 检测杀毒软件</summary>
    private void DoAntivirusDetect()
    {
        SetStatus("正在检测杀毒软件...");
        _logBox.Clear();

        _lastAvItems = _avDetect.Detect();
        var text = _avDetect.FormatResult(_lastAvItems);
        AppendLog(text, LogService.LogLevel.INFO);

        // 提供打开任务管理器的选项
        if (_lastAvItems.Count > 0)
        {
            var open = MessageBox.Show(
                "检测到杀毒软件正在运行。\n\n是否打开任务管理器？\n（本工具不会强制关闭任何进程）",
                "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (open == DialogResult.Yes)
                ProcessHelper.LaunchSystemApp("taskmgr.exe");
        }

        SetStatus("杀毒软件检测完成");
    }

    /// <summary>3. 打开智能应用控制设置</summary>
    private void DoOpenSmartAppControl()
    {
        SetStatus("正在打开智能应用控制设置...");

        _winSecurityOpened = _winSecurity.OpenSmartAppControlSettings();
        var instructions = _winSecurity.GetInstructions();
        AppendLog(instructions, LogService.LogLevel.INFO);

        if (_winSecurityOpened)
            SetStatus("已打开 Windows 安全中心设置页面");
        else
        {
            SetStatus("无法打开设置页面");
            MessageBox.Show(
                "无法自动打开 Windows 安全中心。\n\n请手动打开：\n开始菜单 → Windows 安全中心 → 应用和浏览器控制",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>4. 启用 .NET Framework 3.5</summary>
    private async Task DoDotNet35Enable()
    {
        var confirm = MessageBox.Show(
            "即将启用 Windows 功能 .NET Framework 3.5。\n\n" +
            "该操作需要管理员权限，可能需要联网或 Windows 安装源。\n\n是否继续？",
            "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        SetButtonsEnabled(false);
        SetStatus("正在启用 .NET Framework 3.5（可能需要几分钟）...");
        _logBox.Clear();
        _log.Log("开始执行 DISM 启用 .NET Framework 3.5...");

        _lastDotnetResult = await Task.Run(() =>
        {
            return _dotnet35.Enable(line =>
            {
                if (InvokeRequired)
                    BeginInvoke(() => AppendLog(line, LogService.LogLevel.INFO));
                else
                    AppendLog(line, LogService.LogLevel.INFO);
            });
        });

        if (_lastDotnetResult.Success)
        {
            _log.Log(_lastDotnetResult.Summary, LogService.LogLevel.SUCCESS);
            SetStatus(".NET Framework 3.5 启用成功");
        }
        else
        {
            _log.Log(_lastDotnetResult.Summary, LogService.LogLevel.ERROR);
            AppendLog(_dotnet35.GetFailureSuggestions(_lastDotnetResult.ExitCode), LogService.LogLevel.WARN);
            SetStatus(".NET Framework 3.5 启用失败");

            // 提供手动打开 Windows 功能的选项
            var openManual = MessageBox.Show(
                ".NET Framework 3.5 启用失败。\n\n是否打开 Windows 功能界面手动启用？",
                "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (openManual == DialogResult.Yes)
                ProcessHelper.LaunchSystemApp("OptionalFeatures.exe");
        }

        SetButtonsEnabled(true);
    }

    /// <summary>5. 解除 Siemens 重启提示</summary>
    private void DoSiemensRebootFix()
    {
        SetStatus("正在检测重启挂起状态...");
        _logBox.Clear();

        _lastRebootResult = _rebootFix.Detect();
        AppendLog(_rebootFix.FormatDetectResult(_lastRebootResult), LogService.LogLevel.INFO);

        if (!_lastRebootResult.PendingFound)
        {
            SetStatus("未检测到重启挂起标记");
            return;
        }

        // 第一次确认
        var confirm1 = MessageBox.Show(
            "检测到系统存在待重启状态。\n\n" +
            "该状态可能导致 Siemens / TIA Portal 安装时反复提示重启。\n\n" +
            "建议优先正常重启电脑。如果确认需要处理，点击"是"继续。",
            "检测到重启挂起", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm1 != DialogResult.Yes)
        {
            SetStatus("用户取消操作");
            return;
        }

        // 第二次确认
        var confirm2 = MessageBox.Show(
            _rebootFix.GetConfirmationWarning(),
            "二次确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm2 != DialogResult.Yes)
        {
            SetStatus("用户取消操作");
            return;
        }

        // 备份
        SetStatus("正在备份注册表...");
        if (!_rebootFix.Backup(_lastRebootResult))
        {
            MessageBox.Show("注册表备份失败，操作已中止。", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("备份失败，操作中止");
            return;
        }
        _log.Log($"备份已保存到: {_lastRebootResult.BackupPath}");

        // 删除值
        SetStatus("正在处理注册表值...");
        _rebootFix.DeletePendingValue(_lastRebootResult);

        if (_lastRebootResult.Deleted)
        {
            AppendLog(_lastRebootResult.Summary, LogService.LogLevel.SUCCESS);
            SetStatus("重启挂起标记已处理");
            MessageBox.Show(
                "已处理完成。\n\n" + _lastRebootResult.Summary,
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            AppendLog(_lastRebootResult.Summary, LogService.LogLevel.ERROR);
            SetStatus("处理失败");
        }
    }

    /// <summary>6. 打开卸载工具</summary>
    private void DoOpenUninstallTool()
    {
        // 检查文件是否存在
        var toolPath = _uninstallTool.LocateGeekUninstaller();
        if (toolPath == null)
        {
            _log.Log("未找到卸载工具", LogService.LogLevel.ERROR);
            MessageBox.Show(
                "未找到卸载工具，请确认 tools 目录下存在 Geek Uninstaller.exe。",
                "文件缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 安全确认
        var confirm = MessageBox.Show(
            _uninstallTool.GetLaunchConfirmation(),
            "安全确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        // 启动
        _lastUninstallResult = _uninstallTool.LaunchGeekUninstallerAsAdmin();

        if (_lastUninstallResult.LaunchSuccess)
        {
            SetStatus("卸载工具已启动");
        }
        else
        {
            MessageBox.Show(
                $"卸载工具启动失败。\n\n{_lastUninstallResult.ErrorMessage}",
                "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("卸载工具启动失败");
        }
    }

    /// <summary>7. 导出系统准备报告</summary>
    private void DoExportReport()
    {
        SetStatus("正在生成系统准备报告...");

        var path = _report.GenerateReport(
            _lastSysResult,
            _lastAvItems,
            _lastDotnetResult,
            _lastRebootResult,
            _lastUninstallResult,
            _winSecurityOpened);

        SetStatus("报告已生成");

        var open = MessageBox.Show(
            $"系统准备报告已保存到:\n{path}\n\n是否立即打开？",
            "导出成功", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (open == DialogResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
