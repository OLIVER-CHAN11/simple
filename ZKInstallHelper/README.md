# 智控安装助手 (ZKInstallHelper)

工业软件安装环境检测与修复工具，帮助用户在安装大型 Windows 软件前完成环境检测、VC++ 运行库修复、安装包启动、日志收集和诊断报告导出。

## 功能列表

| 功能 | 说明 |
|------|------|
| 一键检测电脑环境 | Windows 版本、磁盘、VC++、杀毒软件等全面检测 |
| 一键修复 VC++ 运行库 | 自动下载微软官方 x64/x86 运行库并静默安装 |
| 清理安装缓存 | 安全清理 TEMP 目录，不删除系统关键文件 |
| 选择安装包目录 | 自动识别 Start.exe / Setup.exe 安装入口 |
| 开始安装 | 以管理员权限启动官方安装程序 |
| 打开安装日志 | 快速打开 Siemens 安装日志目录 |
| 导出诊断报告 | 生成完整 TXT 诊断报告，含建议处理方案 |
| 远程协助入口 | 引导用户发送诊断报告给客服 |
| 微软卸载修复工具 | 打开微软官方页面清理 VC++ 残留 |

## 重要声明

- **不实现破解功能**
- **不实现自动激活功能**
- **不内置、不分发破解补丁、破解器、盗版许可证**
- 本软件只做合法的安装环境检测、运行库修复、安装引导、日志收集
- 用户需要自行准备合法安装包或正版授权安装包

## 技术栈

- C# WinForms
- .NET 8 (net8.0-windows)
- 目标系统: Windows 10 / Windows 11 (64 位)

## 编译方法

### 前置条件

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 编译

```bash
cd ZKInstallHelper
dotnet build -c Release
```

### 发布为单文件 EXE

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

发布后在 `./publish` 目录下得到 `ZKInstallHelper.exe`，可直接分发。

### 发布为依赖框架的轻量版

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish-lite
```

此版本体积更小，但需要目标机器已安装 .NET 8 Runtime。

## 运行

1. 双击 `ZKInstallHelper.exe`
2. 如果不是管理员权限，会自动请求提升（UAC 弹窗）
3. 所有操作日志保存在 `logs/` 目录

## 目录结构

```
ZKInstallHelper/
├─ ZKInstallHelper.exe        # 主程序
├─ config/
│  └─ manifest.json           # 配置文件（下载地址等）
├─ downloads/                 # 下载的运行库安装包
├─ logs/                      # 日志文件
│  ├─ app.log                 # 主日志
│  ├─ system_check.log        # 系统检测日志
│  ├─ vc_install_x64.log      # VC++ x64 安装日志
│  ├─ vc_install_x86.log      # VC++ x86 安装日志
│  └─ install_action.log      # 安装操作日志
├─ reports/                   # 导出的诊断报告
└─ tools/                     # 预留工具目录
```

## 代码结构

```
ZKInstallHelper/
├─ Program.cs                 # 入口，管理员检测
├─ MainForm.cs                # 主窗体 UI + 事件
├─ Services/
│  ├─ LogService.cs           # 日志服务
│  ├─ SystemCheckService.cs   # 系统环境检测
│  ├─ VCRuntimeService.cs     # VC++ 检测与安装
│  ├─ DownloadService.cs      # 文件下载
│  ├─ InstallerService.cs     # 安装包管理
│  ├─ CleanupService.cs       # 缓存清理
│  └─ ReportService.cs        # 诊断报告
├─ Models/
│  ├─ SystemCheckResult.cs    # 检测结果模型
│  ├─ VCRuntimeResult.cs      # VC++ 结果模型
│  └─ AppConfig.cs            # 配置模型
├─ Utils/
│  ├─ RegistryHelper.cs       # 注册表工具
│  ├─ FileHelper.cs           # 文件工具
│  └─ ProcessHelper.cs        # 进程工具
└─ Properties/
   └─ app.manifest            # 管理员权限清单
```

## 配置说明

`config/manifest.json` 支持自定义下载地址：

```json
{
  "appVersion": "1.0.0",
  "remoteSupportUrl": "",
  "files": [
    {
      "name": "vc_redist.x64.exe",
      "url": "https://aka.ms/vs/17/release/vc_redist.x64.exe",
      "type": "runtime",
      "arch": "x64"
    }
  ]
}
```

后续可扩展为从 OSS/COS/CDN 下载，或添加版本更新检测。

## 许可

MIT License
