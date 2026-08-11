# MiTV Remote

Windows 系统托盘遥控器，支持小米/Redmi 显示器与电视，通过局域网 HTTP 接口（端口 6095）控制设备。

## 功能

- 系统托盘常驻与遥控器弹窗
- 音量百分比设置，签名接口不可用时自动回退到音量按键
- HDMI 1/2 切换、电源、方向键、主页、返回、菜单
- 亮度 OSD 菜单调节
- 可取消的局域网设备发现
- 配置保存到 `%AppData%\MiTVRemote\config.json`

## 系统要求

- Windows 10 或更高版本
- .NET 8 Desktop Runtime；GitHub Actions 产物是 framework-dependent 发布包

## 本地构建

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)，然后在 PowerShell 执行：

```powershell
.\script\build_windows.ps1 -NoRun
```

发布文件位于 `dist\windows\MiTV-Remote.exe`。使用 `-Configuration Debug` 可以生成 Debug 版本。

也可以直接执行：

```powershell
dotnet build MiTVRemoteWin.sln -c Release
dotnet publish src\MiTVRemote.WinForms\MiTVRemote.WinForms.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o dist\windows
```

## GitHub Actions

`.github/workflows/windows.yml` 使用 `windows-latest` runner，执行 restore、Release build 和 `win-x64` publish。每次推送到 `feature/windows-platform-v2`、针对 `master` 的 Pull Request 或手动触发时运行。构建完成后可在 Actions 的 Artifacts 下载 `MiTV-Remote-win-x64`。

## 指定设备

默认设备地址为 `192.168.1.50`。可以通过环境变量覆盖：

```powershell
$env:TV_VOLUME_MITV_HOST = "192.168.1.80"
.\dist\windows\MiTV-Remote.exe
```

也可以从托盘菜单执行“搜索/切换设备”，选择结果会持久化。

## 项目结构

```text
MiTVRemoteWin.sln
src/MiTVRemote.WinForms/
├── Controllers/       HTTP 协议与遥控业务
├── Models/            领域模型和结果类型
├── Platform/          配置文件和网络接口枚举
├── UI/                遥控器与设备选择窗体
├── Program.cs         应用入口
└── TrayApplicationContext.cs
.github/workflows/
└── windows.yml        Windows 远程构建
