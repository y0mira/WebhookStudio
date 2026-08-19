# Webhook Studio

[English](README.en.md) | 简体中文 · [操作手册](docs/user-manual.md) · [架构说明](docs/architecture.md)

Webhook Studio 是一个本地优先、开源、单进程运行的 Webhook 调试工具。它在本机创建 Webhook 接收地址，帮助开发者接收、检查、筛选、比较、重放以及导入导出 HTTP 请求，数据无需发送给第三方服务。

> 项目当前没有身份认证，默认只监听 `127.0.0.1`。捕获内容和数据库可能包含密钥或业务数据，请勿直接暴露到公网。

## 它解决什么问题

开发支付通知、消息回调、CI/CD 或第三方平台集成时，通常需要确认对方实际发送了什么。Webhook Studio 可以帮助你：

- 接收并实时查看 Webhook 的 Method、URL、Header、Query 和 Body。
- 保存请求，稍后搜索、筛选或比较两次回调的差异。
- 调整本地服务后重放已经捕获的请求。
- 自定义 Webhook Endpoint 的响应状态、Body 和延迟。
- 在本机保存调试数据，不依赖第三方 Webhook 收集服务。

Webhook Studio 与 Postman、Apifox 的侧重点不同：Postman 和 Apifox 主要用于主动构造并发送 API 请求；Webhook Studio 主要用于被动接收外部系统发送的 Webhook，然后检查、比较和重放。

## 核心功能

### 接收和实时查看

- 使用名称和唯一 Slug 创建本地 Endpoint。
- 接收 GET、POST、PUT、PATCH、DELETE、HEAD 和 OPTIONS。
- 保存路径、查询参数、Header、Body、来源 IP、时间和响应状态。
- 通过 SignalR 将新请求实时推送到浏览器。
- 使用 SQLite 持久化，重启应用后数据仍然存在。

### 检查、筛选和比较

- 查看 JSON、文本和二进制 Body。
- 按 HTTP Method、状态类别、时间和关键词筛选。
- 对两条请求进行结构化比较，区分新增、删除和变化字段。
- 复制请求信息和 curl，导出 HAR 文件。

### 重放和响应配置

- 将捕获请求重放到指定 HTTP 或 HTTPS 地址。
- 显示目标响应状态、耗时和明确的失败原因。
- 为每个 Endpoint 配置状态码、Content-Type、响应 Body、延迟和保留数量。
- 默认阻止本机、私网和危险重定向，降低 SSRF 风险。

### 数据与界面

- Endpoint 数据支持版本化 JSON 导入和导出。
- 简体中文与英文即时切换，语言偏好会保留。
- 深色和浅色主题。
- 适配手机、平板和桌面宽度。

## 如何运行

Windows 发布包解压后双击 `WebhookStudio.exe` 即可。应用成功监听后会自动打开浏览器，默认地址为：

```text
http://127.0.0.1:8090
```

端口和自动打开行为可在 EXE 同目录的 `appsettings.json` 中修改：

```json
{
  "Hosting": {
    "Url": "http://127.0.0.1:8090",
    "OpenBrowserOnStart": true
  }
}
```

发布包包含 React 页面和 .NET Runtime，不需要另外安装 Node.js、Vite 或 .NET Runtime，也不需要分别启动前后端。

完整的 Windows、macOS、Linux、Docker 安装步骤和功能操作见[操作手册](docs/user-manual.md)。

## 支持的平台

| 平台 | 架构 | 发布标识 | 状态 |
|---|---|---|---|
| Windows | Intel/AMD 64 位 | `win-x64` | 已实际运行验收 |
| Windows | ARM 64 位 | `win-arm64` | 已构建，未在对应设备运行 |
| Linux | Intel/AMD 64 位 | `linux-x64` | 已构建，未在对应系统运行 |
| Linux | ARM 64 位 | `linux-arm64` | 已构建，未在对应系统运行 |
| macOS | Apple Silicon（M1/M2/M3/M4） | `osx-arm64` | 已构建，未在真实 Mac 运行 |

目前没有 Intel Mac 的 `osx-x64` 发布包。

## 架构

正式版本只运行一个 ASP.NET Core 进程：

```text
浏览器 ──> React 静态页面 ──┐
                             ├──> ASP.NET Core ──> EF Core ──> SQLite
Webhook 发送方 ──> /hooks/... ┘          │
                                         └──> SignalR 实时通知
```

- 前端：React、TypeScript、Vite。
- 后端：ASP.NET Core 8 Minimal API。
- 数据：SQLite 与 EF Core migrations。
- 实时通信：SignalR。
- 发布：self-contained 单文件程序、Docker 和 Docker Compose。

开发环境可以分别运行 Vite 和后端以获得热更新；正式发布时 React 已由 ASP.NET Core 托管。更多设计细节见[架构说明](docs/architecture.md)。

## 安全边界

- 默认仅监听 loopback，不向局域网或公网开放。
- 重放默认禁止访问 localhost、私网、link-local、multicast、unspecified 和危险重定向。
- DNS 在发送前和实际连接时都会校验，减少 DNS rebinding 风险。
- Authorization、Cookie 等敏感 Header 默认不会随导出或重放发送。
- 捕获 Body 和 Header 不写入应用日志，前端将其作为文本而不是 HTML 渲染。
- SQLite 适合本地单实例，不适合高并发、多实例生产服务。

安全问题请参阅 [SECURITY.md](SECURITY.md)。

## 文档与参与贡献

- [操作手册](docs/user-manual.md)
- [架构说明](docs/architecture.md)
- [贡献指南](CONTRIBUTING.md)
- [安全政策](SECURITY.md)
- [变更记录](CHANGELOG.md)

当前版本为 `0.1.0`，属于 pre-1.0。项目使用 [MIT License](LICENSE)。
