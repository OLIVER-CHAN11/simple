using System;
using System.Drawing;
using System.Windows.Forms;
using ZKInstallHelper.Models;
using ZKInstallHelper.Services;
using ZKInstallHelper.Utils;

namespace ZKInstallHelper;

/// <summary>
/// 主窗体：左侧按钮区 + 右侧日志区
/// </summary>
public class MainForm : Form
{
    // ---- 服务 ----
    private readonly LogService _log = LogService.Instance;
    private readonly SystemCheckService _systemCheck = new();
    private readonly VCRuntimeService _vcRuntime = new();
    private readonly DownloadService _download = new();
    private readonly InstallerService _installer = new();
    private readonly CleanupService _cleanup = new();
    private readonly ReportService _report = new();
    private readonly AppConfig _config;

    // ---- 状态 ----
    private SystemCheckResult? _lastCheckResult;

    // ---- UI 控件 ----
    private Panel _leftPanel = null!;
    private RichTextBox _logBox = null!;
    private ProgressBar _progressBar = null!;
    private Label _statusLabel = null!;

    public MainForm()
    {
        _config = AppConfig.Load();
        InitializeUI();
        RegisterLogCallback();
        _log.Log("智控安装助手已启动（管理员模式）", LogService.LogLevel.SUCCESS);
    }

    // ================================================================
    // UI 初始化
    // ================================================================

    private void InitializeUI()
    {
        // 窗口属性
        Text = "智控安装助手";
        Size = new Size(920, 620);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9f);

        // 左侧按钮面板
        _leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = Color.FromArgb(245, 245, 248),
            Padding = new Padding(10, 15, 10, 10)
        };
        Controls.Add(_leftPanel);

        // 左侧标题
        var titleLabel = new Label
        {
            Text = "🏠 智控安装助手",
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 60),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _leftPanel.Controls.Add(titleLabel);

        // 按钮列表
        string[] buttonTexts =
        {
            "一键检测电脑环境",
            "一键修复 VC++ 运行库",
            "清理安装缓存",
            "选择安装包目录",
            "开始安装",
            "打开安装日志",
            "导出诊断报告",
            "打开远程协助入口",
            "微软卸载修复工具"
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        _leftPanel.Controls.Add(buttonsPanel);
        buttonsPanel.BringToFront();

        for (int i = 0; i < buttonTexts.Length; i++)
        {
            var btn = new Button
            {
                Text = buttonTexts[i],
                Width = 175,
                Height = 38,
                Margin = new Padding(0, 3, 0, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(50, 50, 60),
                Cursor = Cursors.Hand,
                Tag = i
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 215);
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

        // 状态栏
        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 25,
            Text = "就绪",
            ForeColor = Color.FromArgb(80, 80, 90),
            Font = new Font("Microsoft YaHei UI", 9f)
        };
        rightPanel.Controls.Add(_statusLabel);

        // 进度条
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 22,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Style = ProgressBarStyle.Continuous
        };
        rightPanel.Controls.Add(_progressBar);

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
            {
                BeginInvoke(() => AppendLog(message, level));
            }
            else
            {
                AppendLog(message, level);
            }
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

    private void SetProgress(int value)
    {
        if (InvokeRequired)
            BeginInvoke(() => _progressBar.Value = Math.Clamp(value, 0, 100));
        else
            _progressBar.Value = Math.Clamp(value, 0, 100);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        foreach (Control c in _leftPanel.Controls)
        {
            if (c is FlowLayoutPanel flow)
            {
                foreach (Control btn in flow.Controls)
                {
                    if (btn is Button b) b.Enabled = enabled;
                }
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
                case 1: await DoVCRepair(); break;
                case 2: DoCleanup(); break;
                case 3: DoSelectInstaller(); break;
                case 4: DoStartInstall(); break;
                case 5: DoOpenLog(); break;
                case 6: DoExportReport(); break;
                case 7: DoRemoteSupport(); break;
                case 8: DoOpenMSTool(); break;
            }
        }
        catch (Exception ex)
        {
            _log.Log($"操作异常: {ex.Message}", LogService.LogLevel.ERROR);
            MessageBox.Show($"操作过程中发生错误:\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ================================================================
    // 功能实现
    // ================================================================

    /// <summary>一键检测电脑环境</summary>
    private async Task DoSystemCheck()
    {
        SetButtonsEnabled(false);
        SetStatus("正在检测系统环境...");
        SetProgress(0);

        await Task.Run(() =>
        {
            _lastCheckResult = _systemCheck.RunFullCheck();
        });

        var formatted = _systemCheck.FormatResult(_lastCheckResult!);
        _logBox.Clear();
        AppendLog(formatted, LogService.LogLevel.INFO);

        SetProgress(100);
        SetStatus("系统环境检测完成");
        SetButtonsEnabled(true);
    }

    /// <summary>一键修复 VC++ 运行库</summary>
    private async Task DoVCRepair()
    {
        SetButtonsEnabled(false);
        SetStatus("正在下载 VC++ 运行库...");
        SetProgress(0);

        var downloadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
        if (!Directory.Exists(downloadsDir))
            Directory.CreateDirectory(downloadsDir);

        // 下载 x64
        var x64Path = Path.Combine(downloadsDir, "vc_redist.x64.exe");
        var x64Url = _config.Files.FirstOrDefault(f => f.Arch == "x64")?.Url
                     ?? "https://aka.ms/vs/17/release/vc_redist.x64.exe";

        if (!File.Exists(x64Path) || new FileInfo(x64Path).Length < 1024 * 1024)
        {
            var ok = await _download.DownloadFileAsync(x64Url, x64Path, p =>
            {
                SetProgress(p / 2); // 前 50% 给 x64 下载
                SetStatus($"下载 VC++ x64... {p}%");
            });
            if (!ok) { SetButtonsEnabled(true); return; }
        }
        else
        {
            _log.Log("VC++ x64 安装包已存在，跳过下载");
        }

        // 下载 x86
        var x86Path = Path.Combine(downloadsDir, "vc_redist.x86.exe");
        var x86Url = _config.Files.FirstOrDefault(f => f.Arch == "x86")?.Url
                     ?? "https://aka.ms/vs/17/release/vc_redist.x86.exe";

        if (!File.Exists(x86Path) || new FileInfo(x86Path).Length < 1024 * 1024)
        {
            var ok = await _download.DownloadFileAsync(x86Url, x86Path, p =>
            {
                SetProgress(50 + p / 2); // 后 50% 给 x86 下载
                SetStatus($"下载 VC++ x86... {p}%");
            });
            if (!ok) { SetButtonsEnabled(true); return; }
        }
        else
        {
            _log.Log("VC++ x86 安装包已存在，跳过下载");
        }

        // 安装 x64
        SetStatus("正在安装 VC++ x64...");
        SetProgress(0);
        int exitCode64 = 0;
        await Task.Run(() => { exitCode64 = _vcRuntime.Install(x64Path, "x64"); });
        SetProgress(50);

        // 安装 x86
        SetStatus("正在安装 VC++ x86...");
        int exitCode86 = 0;
        await Task.Run(() => { exitCode86 = _vcRuntime.Install(x86Path, "x86"); });
        SetProgress(100);

        // 重新检测
        _log.Log("重新检测 VC++ 运行库状态...");
        var vcx64 = _vcRuntime.CheckRuntime("x64");
        var vcx86 = _vcRuntime.CheckRuntime("x86");
        _log.Log(vcx64.StatusText);
        _log.Log(vcx86.StatusText);

        // 总结
        if (exitCode64 == 0 && exitCode86 == 0)
            SetStatus("VC++ 运行库修复完成");
        else if (exitCode64 == 3010 || exitCode86 == 3010)
            SetStatus("VC++ 修复完成，建议重启电脑");
        else
            SetStatus("VC++ 修复遇到问题，请查看日志");

        SetButtonsEnabled(true);
    }

    /// <summary>清理安装缓存</summary>
    private void DoCleanup()
    {
        SetStatus("正在清理临时文件...");
        var (deleted, skipped, freed) = _cleanup.CleanTempFiles();
        SetStatus($"清理完成：删除 {deleted} 个文件，释放 {FileHelper.FormatBytes(freed)}");
    }

    /// <summary>选择安装包目录</summary>
    private void DoSelectInstaller()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "请选择安装包所在文件夹",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var entry = _installer.SelectAndDetect(dialog.SelectedPath);
            if (entry != null)
                SetStatus($"检测到安装程序: {Path.GetFileName(entry)}");
            else
                SetStatus("未找到安装入口");
        }
    }

    /// <summary>开始安装</summary>
    private void DoStartInstall()
    {
        if (string.IsNullOrEmpty(_installer.DetectedEntry))
        {
            MessageBox.Show("请先点击"选择安装包目录"并确认安装入口。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"即将启动安装程序:\n{_installer.DetectedEntry}\n\n确认开始安装？",
            "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _installer.StartInstall();
            SetStatus("安装程序已启动");
        }
    }

    /// <summary>打开安装日志</summary>
    private void DoOpenLog()
    {
        var siemensDir = @"C:\ProgramData\Siemens\Automation\Logfiles\Setup";
        if (Directory.Exists(siemensDir))
        {
            ProcessHelper.OpenFolder(siemensDir);
            _log.Log($"已打开 Siemens 日志目录: {siemensDir}");
        }
        else
        {
            _log.Log("暂未检测到 Siemens 安装日志目录，可能还没有开始安装，或安装程序未生成日志。",
                LogService.LogLevel.WARN);

            var openLocal = MessageBox.Show(
                "Siemens 安装日志目录不存在。\n\n是否打开本软件的日志目录？",
                "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (openLocal == DialogResult.Yes)
                ProcessHelper.OpenFolder(_log.LogDir);
        }
    }

    /// <summary>导出诊断报告</summary>
    private void DoExportReport()
    {
        SetStatus("正在生成诊断报告...");
        var path = _report.GenerateReport(_lastCheckResult, _installer.SelectedDirectory, _installer.DetectedEntry);
        SetStatus("诊断报告已生成");

        var open = MessageBox.Show(
            $"诊断报告已保存到:\n{path}\n\n是否立即打开？",
            "导出成功", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (open == DialogResult.Yes)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }

    /// <summary>打开远程协助入口</summary>
    private void DoRemoteSupport()
    {
        _log.Log("请先导出诊断报告，再联系远程客服。");
        MessageBox.Show(
            "请将诊断报告发送给客服，由客服判断是否需要远程处理。\n\n" +
            "步骤：\n1. 点击"导出诊断报告"\n2. 将报告文件发送给售后客服\n3. 客服确认后安排远程协助",
            "远程协助", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>打开微软卸载修复工具页面</summary>
    private void DoOpenMSTool()
    {
        _log.Log("正在打开微软官方卸载修复工具页面...");
        _log.Log("当 VC++ 重装时反复提示手动查找 vc_runtimeMinimum_x64.msi，" +
                 "通常是旧的 VC++ 安装缓存或注册信息损坏。");
        _log.Log("请使用微软官方工具清理以下项目：");
        _log.Log("  1. Microsoft Visual C++ 2022 X64 Minimum Runtime");
        _log.Log("  2. Microsoft Visual C++ 2022 X64 Additional Runtime");
        _log.Log("  3. Microsoft Visual C++ 2015-2022 Redistributable x64");
        _log.Log("  4. Microsoft Visual C++ 2015-2022 Redistributable x86");

        ProcessHelper.OpenUrl(
            "https://support.microsoft.com/en-gb/topic/fix-problems-that-block-programs-from-being-installed-or-removed-cca7d1b6-65a9-3d98-426b-e9f927e1eb4d");

        SetStatus("已打开微软卸载修复工具页面");
    }
}
