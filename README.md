# HBR.Payment.WatchDog

一个基于 WPF 的 Windows 进程守护工具，用于监控并自动重启指定的可执行程序。

## 功能

- 监控多个 `.exe` 进程，崩溃后自动重启
- 可配置检查间隔（默认 3 秒）
- 支持手动启动 / 停止 / 重启单个进程
- 支持启用 / 禁用单个监控目标
- 支持调整启动顺序
- 配置持久化到 `watchdog.json`

## 环境要求

- Windows 10/11（x64）
- .NET 9.0 Runtime

## 配置文件

程序目录下的 `watchdog.json`：

```json
{
  "CheckIntervalSeconds": 3,
  "Targets": [
    {
      "StartOrder": 1,
      "Name": "示例程序",
      "ExecutablePath": "C:\\Path\\To\\App.exe",
      "Arguments": "",
      "WorkingDirectory": "",
      "IsEnabled": true
    }
  ]
}
```

| 字段 | 说明 |
|------|------|
| `CheckIntervalSeconds` | 检查间隔（秒），最小值 1 |
| `StartOrder` | 启动顺序 |
| `Name` | 显示名称 |
| `ExecutablePath` | 可执行文件路径（绝对或相对路径） |
| `Arguments` | 启动参数 |
| `WorkingDirectory` | 工作目录（留空则使用 exe 所在目录） |
| `IsEnabled` | 是否启用自动守护 |

## 状态说明

| 状态 | 含义 |
|------|------|
| 运行中（N） | 正在运行，N 为实例数 |
| 已停止 | 未运行 |
| 已手动停止 | 用户手动停止，不会自动重启 |
| 已禁用 | 该目标已禁用，不会自动重启 |
| 程序文件不存在 | 路径无效 |

## 构建

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```
