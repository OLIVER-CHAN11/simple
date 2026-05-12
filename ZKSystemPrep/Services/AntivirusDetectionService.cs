using System;
using System.Diagnostics;
using ZKSystemPrep.Models;
using ZKSystemPrep.Utils;

namespace ZKSystemPrep.Services;

/// <summary>
/// 杀毒软件检测服务
/// 重要原则：只检测，不强制结束，不 kill 进程，不修改设置
/// </summary>
public class AntivirusDetectionService
{
    private readonly LogService _log = LogService.Instance;

    /// <summary>已知杀毒软件进程映射表：进程名 → 品牌名称</summary>
    private static readonly Dictionary<string, string> KnownProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // 360
        { "360tray.exe", "360 安全卫士" },
        { "360sd.exe", "360 杀毒" },
        { "360rp.exe", "360 实时防护" },
        { "ZhuDongFangYu.exe", "360 主动防御" },

        // 腾讯电脑管家
        { "QQPCTray.exe", "腾讯电脑管家" },
        { "QQPCRTP.exe", "腾讯电脑管家" },
        { "QQPCMgr.exe", "腾讯电脑管家" },

        // 火绒
        { "HipsTray.exe", "火绒安全" },
        { "HipsDaemon.exe", "火绒安全" },

        // Windows Defender
        { "MsMpEng.exe", "Windows Defender" },
        { "SecurityHealthSystray.exe", "Windows 安全中心" },

        // 金山毒霸
        { "KSafeTray.exe", "金山毒霸" },
        { "KWatch.exe", "金山毒霸" },

        // 瑞星
        { "RavMonD.exe", "瑞星杀毒" },

        // 卡巴斯基
        { "avp.exe", "卡巴斯基" },

        // ESET
        { "egui.exe", "ESET NOD32" },
        { "ekrn.exe", "ESET NOD32" },

        // Avast
        { "AvastUI.exe", "Avast" },
        { "AvastSvc.exe", "Avast" },

        // AVG
        { "AVGUI.exe", "AVG" },

        // Bitdefender
        { "bdagent.exe", "Bitdefender" },

        // McAfee
        { "McUICnt.exe", "McAfee" },

        // Norton
        { "NortonSecurity.exe", "Norton" },
        { "NSWscSvc.exe", "Norton" },
    };

    /// <summary>扫描当前正在运行的杀毒软件进程</summary>
    public List<AntivirusProcessItem> Detect()
    {
        _log.LogTo("antivirus_check.log", "========== 开始检测杀毒软件 ==========");
        var detected = new List<AntivirusProcessItem>();
        var foundBrands = new HashSet<string>();

        foreach (var (processName, brandName) in KnownProcesses)
        {
            try
            {
                var pids = ProcessHelper.GetProcessIds(processName);
                if (pids.Length > 0)
                {
                    foreach (var pid in pids)
                    {
                        detected.Add(new AntivirusProcessItem
                        {
                            BrandName = brandName,
                            ProcessName = processName,
                            Pid = pid
                        });
                    }
                    if (!foundBrands.Contains(brandName))
                    {
                        foundBrands.Add(brandName);
                        _log.LogTo("antivirus_check.log", $"检测到: {brandName} ({processName}, PID: {string.Join(",", pids)})");
                    }
                }
            }
            catch { }
        }

        if (detected.Count == 0)
            _log.LogTo("antivirus_check.log", "未检测到常见杀毒软件进程");

        _log.LogTo("antivirus_check.log", "========== 杀毒软件检测完成 ==========");
        return detected;
    }

    /// <summary>格式化检测结果为显示文本</summary>
    public string FormatResult(List<AntivirusProcessItem> items)
    {
        var lines = new List<string>
        {
            "━━━━━━━━━━ 杀毒软件检测结果 ━━━━━━━━━━",
            ""
        };

        if (items.Count == 0)
        {
            lines.Add("✅ 未检测到常见杀毒软件进程正在运行。");
        }
        else
        {
            lines.Add("检测到以下安全软件正在运行：");
            lines.Add("");

            // 按品牌去重显示
            var brands = items.Select(i => i.BrandName).Distinct().ToList();
            foreach (var brand in brands)
            {
                var procs = items.Where(i => i.BrandName == brand)
                                 .Select(i => i.ProcessName)
                                 .Distinct();
                lines.Add($"  ⚠️ {brand}：{string.Join(", ", procs)}");
            }

            lines.Add("");
            lines.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            lines.Add("");
            lines.Add("建议：");
            lines.Add("安装 Siemens / TIA Portal 前，请根据实际情况");
            lines.Add("手动退出或临时暂停防护，避免安装包被拦截。");
            lines.Add("");
            lines.Add("⚠️ 本工具不会强制关闭杀毒软件。");
            lines.Add("如需操作，请点击下方"打开任务管理器"自行处理。");
        }

        lines.Add("");
        lines.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>获取检测到的品牌名称列表（用于报告）</summary>
    public List<string> GetDetectedBrandNames(List<AntivirusProcessItem> items)
    {
        return items.Select(i => i.BrandName).Distinct().ToList();
    }
}
