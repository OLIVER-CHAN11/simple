# 智控安装助手 - 系统准备工具 (ZKSystemPrep)

Siemens TIA Portal / 博途安装前环境准备工具，用于检测系统兼容性、修复常见安装阻碍问题。

## 功能列表

| 按钮 | 功能 | 说明 |
|------|------|------|
| 检查系统兼容性 | CPU/内存/磁盘/分辨率/版本/路径检测 | 对照 TIA Portal 官方要求判断 |
| 检测杀毒软件 | 扫描 14 个品牌 34 个进程 | 只检测不关闭，提供打开任务管理器入口 |
| 打开智能应用控制设置 | 打开 Windows 安全中心页面 | 不直接修改任何安全设置 |
| 启用 .NET Framework 3.5 | 调用 DISM 启用 | 实时显示输出，失败提供手动方案 |
| 解除 Siemens 重启提示 | 处理 PendingFileRenameOperations | 备份注册表 + 二次确认后才删除 |
| 打开卸载工具 | 启动 Geek Uninstaller.exe | 不自动卸载，由用户手动选择 |
| 导出系统准备报告 | 生成完整 TXT 诊断报告 | 含建议下一步操作 |

## 安全声明

- **不强制结束**杀毒软件进程
- **不绕过** Windows 安全中心 / Defender / Smart App Control
- **不执行**破解、激活、密钥安装相关功能
- **不自动删除**客户电脑里的程序或注册表
- 注册表删除操作**必须先备份并二次确认**
- 卸载工具**只负责启动**，不自动卸载任何软件
- 所有操作**全部记录日志**

## 技术栈

- C# WinForms
- .NET 8 (`net8.0-windows`)
- 目标系统: Windows 10 / Windows 11 (64 位)

## 编译方法

### 前置条件

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 编译

```bash
cd ZKSystemPrep
dotnet build -c Release
```

### 发布为单文件 EXE（推荐）

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

发布后在 `./publish` 目录得到 `ZKSystemPrep.exe`，可直接分发。

### 发布为依赖框架的轻量版

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish-lite
```

体积更小，但需要目标机器已安装 .NET 8 Runtime。

## 运行

1. 双击 `ZKSystemPrep.exe`
2. 自动请求管理员权限（UAC 弹窗）
3. 拒绝管理员权限时会提示并退出

## 目录结构

```
ZKSystemPrep/
├── ZKSystemPrep.exe              # 主程序
├── tools/
│   └── Geek Uninstaller.exe      # 第三方卸载工具（需自行放入）
├── logs/
│   ├── app.log                   # 主日志
│   ├── system_check.log          # 系统检测日志
│   ├── antivirus_check.log       # 杀毒软件检测日志
│   ├── windows_security.log      # 安全中心操作日志
│   ├── dotnet35.log              # .NET 3.5 启用日志
│   ├── reboot_fix.log            # 重启修复日志
│   └── uninstall_tool.log        # 卸载工具日志
├── backups/
│   └── registry/                 # 注册表备份（.reg 文件）
└── reports/
    └── 系统准备报告_时间.txt      # 导出的诊断报告
```

## 代码结构

```
ZKSystemPrep/
├── Program.cs                          # 入口，管理员权限检测
├── MainForm.cs                         # 主窗体 + 7 个按钮事件
├── Services/
│   ├── LogService.cs                   # 统一日志
│   ├── AdminService.cs                 # 管理员权限检测
│   ├── SystemCompatibilityService.cs   # 系统兼容性检测
│   ├── AntivirusDetectionService.cs    # 杀毒软件检测
│   ├── WindowsSecurityService.cs       # 安全中心页面打开
│   ├── DotNet35Service.cs              # .NET 3.5 启用
│   ├── SiemensRebootFixService.cs      # 重启提示修复
│   ├── UninstallToolService.cs         # 卸载工具启动
│   └── ReportService.cs               # 报告导出
├── Models/
│   ├── SystemCompatibilityResult.cs    # 兼容性结果
│   ├── AntivirusProcessItem.cs         # 杀毒进程信息
│   ├── DotNetEnableResult.cs           # .NET 启用结果
│   ├── RebootFixResult.cs              # 重启修复结果
│   └── UninstallToolResult.cs          # 卸载工具结果
├── Utils/
│   ├── RegistryHelper.cs               # 注册表工具
│   ├── ProcessHelper.cs                # 进程工具
│   └── FileHelper.cs                   # 文件工具
└── Properties/
    └── app.manifest                    # 管理员权限清单
```

## 使用 Geek Uninstaller

本软件不内置 Geek Uninstaller。请自行下载并放置到 `tools/` 目录：

1. 官网下载: https://geekuninstaller.com/
2. 解压后将 `geek.exe` 重命名为 `Geek Uninstaller.exe`
3. 放入 `tools/` 目录

## 注意事项

1. **首次使用建议先点"检查系统兼容性"**，确认系统满足安装要求
2. **解除重启提示**操作会修改注册表，已做备份机制，但仍建议操作前手动创建系统还原点
3. **.NET 3.5 启用**需要网络连接或 Windows 安装源文件
4. **杀毒软件检测**仅做提示，不会强制关闭任何进程
5. **报告文件**适合发送给售后客服用于远程诊断

## 许可

MIT License
