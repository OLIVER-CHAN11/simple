using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using ZKInstallHelper.Services;

namespace ZKInstallHelper;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // 检测管理员权限
        if (!IsRunAsAdmin())
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(startInfo);
                return;
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "当前操作需要管理员权限，请右键以管理员身份运行。",
                    "智控安装助手",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        // 确保目录结构
        EnsureDirectories();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static bool IsRunAsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void EnsureDirectories()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] dirs = { "config", "downloads", "logs", "reports", "tools" };
        foreach (var dir in dirs)
        {
            var path = Path.Combine(baseDir, dir);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
