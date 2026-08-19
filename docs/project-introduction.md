# Webhook Studio 项目介绍

## 1. 项目定位

Webhook Studio 是一个本地优先、开源、单进程运行的 Webhook 调试工具。它在本机创建临时或长期使用的 Webhook 接收地址，帮助开发者接收、检查、筛选、比较、重放以及导入导出 HTTP 请求。

它适合以下场景：

- 开发支付、消息通知、CI/CD 或第三方平台的 Webhook 回调。
- 检查对方实际发送的 Method、URL、Header、Query 和 Body。
- 比较两次回调在字段或内容上的差异。
- 调整本地接口后重放已捕获的请求。
- 在不把请求数据上传到第三方服务的情况下完成调试。

Webhook Studio 与 Postman、Apifox 的区别是：Postman 和 Apifox 主要用于主动构造并发送 API 请求；Webhook Studio 主要用于被动接收外部系统发来的 Webhook，再进行检查、比较和重放。

## 2. 核心能力

### Endpoint 与请求捕获

- 使用名称和唯一 Slug 创建本地 Endpoint。
- 接收 GET、POST、PUT、PATCH、DELETE、HEAD 和 OPTIONS 请求。
- 保存请求路径、查询参数、Header、Body、来源 IP、接收时间和响应状态。
- 通过 SignalR 将新请求实时推送到浏览器。
- 使用 SQLite 在应用重启后保留 Endpoint 和请求。

### 检查、筛选和比较

- 查看文本、JSON 和二进制 Body。
- 按 HTTP Method、状态类别、时间和关键词筛选。
- 选取两条请求进行结构化比较，区分新增、删除和变化字段。
- 复制请求信息或 curl 命令，导出 HAR 文件。

### 请求重放

- 将已捕获请求重放到指定 HTTP 或 HTTPS 地址。
- 显示目标响应状态、耗时和失败原因。
- 默认阻止本机、私网和危险重定向，降低 SSRF 风险。
- 私网重放只能通过显式配置启用，启用后界面持续显示安全警告。

### Endpoint 行为配置

每个 Endpoint 可以独立设置：

- 返回状态码。
- Content-Type。
- 响应 Body。
- 响应延迟。
- 请求保留数量。

### 数据交换与界面

- Endpoint 请求可导出为带格式版本号的 JSON，并重新导入。
- 支持简体中文和英文即时切换。
- 支持深色和浅色主题。
- 页面适配手机、平板和桌面宽度。

## 3. 技术架构

正式发布版本只运行一个 ASP.NET Core 进程：

```text
浏览器 ──> React 静态页面 ──┐
                             ├──> ASP.NET Core ──> EF Core ──> SQLite
Webhook 发送方 ──> /hooks/... ┘          │
                                         └──> SignalR 实时通知
```

- 前端：React、TypeScript、Vite。
- 后端：ASP.NET Core 8 Minimal API。
- 数据库：SQLite 与 EF Core migrations。
- 实时通信：SignalR。
- 发布：self-contained 单文件程序、Docker 和 Docker Compose。

开发环境可以分别启动 Vite 和后端以获得热更新；正式发布时 React 已构建到 ASP.NET Core 的 `wwwroot`，最终用户不需要安装 Node.js 或 .NET Runtime。

## 4. 支持的平台

当前发布目标包括：

| 平台 | 处理器架构 | 发布标识 |
|---|---|---|
| Windows | Intel/AMD 64 位 | `win-x64` |
| Windows | ARM 64 位 | `win-arm64` |
| Linux | Intel/AMD 64 位 | `linux-x64` |
| Linux | ARM 64 位 | `linux-arm64` |
| macOS | Apple Silicon（M1/M2/M3/M4） | `osx-arm64` |

目前没有 Intel Mac 使用的 `osx-x64` 发布包。除 Windows x64 外，其他平台包已完成构建和结构检查，但仍应在对应操作系统上进行运行验收。

## 5. 数据与安全边界

- 默认只监听 loopback，不对局域网或公网开放。
- 项目当前没有账号和身份认证，不应直接暴露到公网。
- 捕获内容可能包含 Authorization、Cookie、签名或业务数据，应按敏感信息保护数据库和导出文件。
- 捕获 Body 和 Header 不写入应用日志，界面将其作为文本而非 HTML 渲染。
- 重放默认过滤敏感 Header，并限制超时、重定向次数和响应大小。
- SQLite 适合本地单实例使用，不适合高并发、多实例生产服务。

## 6. 当前版本范围

当前版本为 `0.1.0`，属于 pre-1.0。它是本地 Webhook 调试器，不包含账号系统、团队协作、云同步、公网隧道、多租户或脚本执行功能。公开 API、数据库和导入格式的兼容性变化会记录在变更日志中。

详细使用步骤见[操作手册](user-manual.md)，系统内部设计见[架构说明](architecture.md)。
