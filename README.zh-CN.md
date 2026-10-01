# Rei Cast — D-Cast 水冷小屏助手

[English](README.md) · [兼容性](COMPATIBILITY.md) · [扩展接口](EXTENSIONS.md) · [MIT 许可](LICENSE)

让水冷上的屏幕成为安静、轻量的桌面助手：工作时显示 Codex 本地任务，收到 Dot 发布的消息时显示最新对话，空闲时显示时钟、天气和电脑性能。

适配以 **DeepCreative 的 D-Cast 已将小屏暴露为 Windows 扩展显示器** 为前提。程序不再绑定 LT360 的型号或 854×480 分辨率，支持手动选择其他 D-Cast 屏幕，自动使用横屏、竖屏或方形布局，并等比缩放头像和内容。

![横屏界面，原创像素机器人和示例数据](assets/preview-landscape.png)

## 功能

- Codex 本地任务名称、思考／执行／等待阶段、运行时间、工具操作数和活跃任务数。
- Dot 发布的最近一条用户消息与可见回复，长回复自动翻页，默认 5 分钟后恢复原有画面。
- 日期时间、可配置城市的天气、CPU／内存／GPU 使用率和网络流量。
- 自动识别单个 D-Cast／DeepCool／JZFS 显示设备，或手动选择任意非主屏；保存显示器标识，帮助应对 Windows 编号变化。
- 普通用户登录自启动、30 秒延迟兜底、重复启动合并、启动诊断与有限次数的唤醒恢复。
- 原创像素头像、可替换本地图像、配置热更新和 JSON 扩展接口。

采用 Windows 原生 C# / WinForms 与 .NET Framework，画面每秒最多刷新一次，性能默认每 3 秒采样，天气默认 30 分钟更新。小屏程序不调用大模型。v0.2.0 在原有小屏上的 20 秒短测约占整机 CPU 0.015%、工作集内存 66 MB；这只是样本，不是峰值保证。

## 支持范围

**面向所有支持 D-Cast 扩展显示模式的水冷屏幕。** 型号不作为限制：只要它在 Windows 中成为非主扩展屏，就可手动选择并显示。其他屏幕尺寸、横竖屏和方屏已做软件布局测试。

目前实机验证限于 LT360 VISION。其他 D-Cast 型号的物理输出仍需要社区反馈，不宣传为“全部型号已经实测”。只有数码管、仅能上传图片/GIF、无法成为 Windows 扩展屏的设备不属于此接口。详见 [兼容性表](COMPATIBILITY.md)。

目标环境是 Windows 10/11 x64 与 .NET Framework 4.8。项目独立于 DeepCool 和 OpenAI。

## 安装

1. 在 [DeepCreative](https://downloads.deepcool.com/) 中开启 D-Cast，在 Windows 中选择扩展显示。
2. 从 [Releases](https://github.com/Cartmancxx/Rei-Cast/releases) 下载 `rei-cast-windows-x64.zip` 并解压。
3. 在解压后的 `rei-cast` 文件夹打开 PowerShell，运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

程序安装到 `%USERPROFILE%\ReiCast\app`，配置保存在其上级目录。安装时保留已有设置，启用登录自启动并打开小屏。这个物理用户目录可避免 MSIX 对 LocalAppData 的重定向导致启动找不到文件。

没有自动识别到屏幕时，右键托盘的 Rei Cast 图标 → 设置，按显示器名称和分辨率选择 D-Cast 屏幕。多个匹配屏幕也需要手动选择。程序不自动占用未知的副屏，也不会选择主屏。

`ReiCast.exe --preview` 可在主屏预览。`scripts/install.ps1 -NoAutostart` 安装但不开启登录自启动。更新个人版时，已有的私人绫波丽头像保持本地使用，公开包只包含原创机器人。

## Dot 对话显示

需要 Your dot 已获准访问这台电脑，并保持在线。请在 Dot 对话里给出持续指令，例如：

> 以后每次回复，请使用 Rei Cast 的 dot-display 技能，把我最新的消息和你实际发送的可见回复发布到这台电脑的水冷小屏。默认显示5分钟，离线时跳过。现在发布一条真实回复验证。

这是 **Dot 主动发布的本地桥接**。打开显示选项并不会自动订阅 Dot 云端对话，也不能保证 Dot 每个回合都调用技能，需用真实回复验证。具体输入格式、大小限制和本地脚本见 [扩展接口](EXTENSIONS.md)。

<img src="assets/preview-portrait.png" width="240" alt="竖屏 Dot 示例"> <img src="assets/preview-compact.png" width="240" alt="方屏 Codex 示例">

## 设置与扩展

托盘设置支持选屏、天气城市与坐标、亮度、秒数、任务名称和 GPU 采样。其他选项可修改 `%USERPROFILE%\ReiCast\config.json`，约 10 秒内生效：

- `layout`：`auto` / `landscape` / `portrait` / `compact`，默认自动。
- `avatarPath`：自己的本地 PNG/JPEG 完整路径。
- `enableDotDialogue`：是否显示 Dot 快照。
- `dialoguePageSeconds`：翻页间隔，5–30 秒。
- `metricsSeconds` / `taskSeconds` / `weatherMinutes`：采样和刷新频率，最低为 2 秒／2 秒／15 分钟。

默认天气城市为成都，城市与经纬度需一起修改。天气由 Open-Meteo 提供，网络请求包含配置的坐标；任务和对话快照在本地处理。性能数据是使用率，不是水冷温度、泵速或风扇遥测。

## 编译

无需 NuGet、Node 或 Python。使用 Windows 自带的 .NET Framework 编译器：

```powershell
.\scripts\build.ps1
.\scripts\verify.ps1
.\scripts\package.ps1
```

GitHub Actions 会自动编译、检查并打包。`main` 首次出现一个新版本，或推送版本标签时，会提供 Windows ZIP 和 SHA-256 校验文件；已有版本的安装包会保留。

## 排查

息屏后黑屏时，先用托盘恢复命令或 `scripts/repair.ps1`。如果窗口心跳正常但实物仍黑屏，请在 DeepCreative 中重新开关 D-Cast；窗口正常绘制不证明厂商链路正在传输画面。

自启动诊断位于 `%USERPROFILE%\ReiCast\startup-launch.json` 与 `runtime.json`。程序在登录后启动，不会在登录前运行或唤醒已关机的电脑。删除安装可用 `scripts/uninstall.ps1`，个人配置会保留。

Codex 任务读取依赖当前本地日志格式；云端任务需要显式发布扩展快照。长期无更新的任务显示为等待状态，不会假装完成。

代码与原创头像使用 MIT 许可。欢迎反馈其他型号、分辨率、方向、Windows 和 DeepCreative 版本，以及自动识别是否成功；请不要提交真实对话、凭据或完整日志。详见 [贡献说明](CONTRIBUTING.md) 与 [官方上游调查](UPSTREAM.md)。
