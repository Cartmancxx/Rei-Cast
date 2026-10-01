# 扩展接口 v1

配置、任务来源、系统指标、天气和绘制分别位于 `Config`、`CodexProvider`、`MetricsProvider`、`WeatherProvider`、`Display` 类，均有源码，可以单独替换。没有需要常驻的 Node/Python 服务。

## 外部任务状态

把状态写入 `%USERPROFILE%\ReiCast\external-task.json`。使用同目录临时文件然后替换的方式原子写入，文件不得超过 8 KB。不需要开放网络端口。

```json
{
  "state": "running",
  "title": "正在导出视频",
  "phase": "渲染中",
  "updatedUtc": "2026-09-25T05:00:00Z",
  "ttlSeconds": 60
}
```

- `state`：`running` / `waiting` / `complete` / `interrupted`。
- `updatedUtc`：实际更新时间，UTC ISO 8601。示例时间必须替换；过期状态会自动忽略，超过当前时间 30 秒的记录也忽略。
- `ttlSeconds`：5–3600 秒。生产者在任务期间定期刷新，进程崩溃后记录不会一直假装运行。
- `title` / `phase`：纯显示文字，不会作为命令执行。

外部任务作为一个额外任务，与 Codex 本地任务按最近更新时间选择展示。这个接口不支持百分比。接入多任务、传感器温度、音乐播放或主机灯光状态时，可新增独立的 JSON 生产者和显示模块；不要直接在屏幕绘制循环里调用耗时命令。

## Dot 对话快照

`scripts/publish-dot.ps1 -InputFile <UTF-8 JSON 文件绝对路径>` 将以下输入原子写入 `%USERPROFILE%\ReiCast\dot-dialogue.json`：

```json
{
  "userMessage": "最近一条用户消息",
  "reply": "Dot 实际发送的可见回复全文",
  "ttlSeconds": 300
}
```

生产者限长：用户消息 600 字符、回复 4000 字符、文件 16 KB。不得把用户内容拼入命令行；先用文件工具写 JSON，再将路径传给脚本。脚本添加唯一 `id`、真实 UTC `updatedUtc` 和 `source: dot`。测试输入可显式使用 `source: demo`，画面会显示“桥接测试”。TTL 10–1800 秒；默认 300 秒。超长原文可发布明确标为节选的内容，全文仍在 Dot 对话中。

小屏仅在文件改变时解析，沿用 2 秒检查；过期、未来时间、超限或无效输入自动忽略。长回复按可绘制区域分页；每页停留时间由配置 `dialoguePageSeconds` 控制。UTF-8 文本只是显示数据，不执行任何内容。

Dot 需要已允许访问且在线的电脑，以及用户在 Dot 中明确给出的持续发布指令。此文件接口不提供读取云端聊天的能力，也不能替代 Dot 的授权或强制每次回复调用技能。

## 可选升级方向

1. 用官方 Codex Hooks 发布事件快照，减少对本地日志格式的依赖。安装和信任需使用 Codex 提供的审核入口。
2. 接入已经安装的硬件监控程序提供的共享内存／本地导出数据，避免再加载硬件驱动。
3. 头像表情可按状态选择多张小图，按秒切换；不必改成高帧率动画。
4. 如加入语音或对话，单独做按需启动的进程，闲时退出，保持屏幕常驻程序轻量。
