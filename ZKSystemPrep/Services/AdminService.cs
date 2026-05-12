using System.Security.Principal;

namespace ZKSystemPrep.Services;

/// <summary>
/// 管理员权限检测服务
/// </summary>
public static class AdminService
{
    /// <summary>检测当前进程是否以管理员权限运行</summary>
    public static bool IsRunAsAdmin()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
