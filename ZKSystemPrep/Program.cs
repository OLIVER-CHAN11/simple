using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using ZKSystemPrep.Services;

namespace ZKSystemPrep;

internal static class Program
{
    [STAThread]
    static void Main()
    {
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
            catch
            {
                MessageBox.Show(
                    "本工具需要管理员权限才能检测系统组件和执行修复操作，请右键以管理员身份运行。",
                    "智控安装助手 - 系统准备工具",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

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
        string[] dirs = { "tools", "logs", "backups/registry", "reports" };
        foreach (var dir in dirs)
        {
            var path = Path.Combine(baseDir, dir);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
