# Webhook Studio

[English](README.en.md) | 简体中文

Webhook Studio 是本地优先、单进程运行的开源 Webhook 调试器：接收、检查、过滤、比较、重放和导入导出请求，无需把数据发送给第三方。

> 本项目没有认证系统，默认仅监听 `127.0.0.1`。捕获内容和数据库可能包含密钥，请按敏感数据保护。不要直接暴露到公网。

## 功能

- 任意本地 Endpoint、SQLite 持久化和 SignalR 实时请求流
- 方法、状态、时间、路径、查询参数和文本 Body 的后端过滤
- Header、JSON、文本和二进制安全检查；结构化 JSON 比较
- 可配置响应、异步延迟、保留策略、curl/HAR/版本化 JSON
- 默认脱敏敏感 Header，默认阻止本机、私网和危险重定向重放
- 深浅主题，简体中文/英文即时切换，375px 至桌面响应式界面

截图尚未提交。拍摄步骤：以正式单进程模式启动，创建两个无敏感数据的示例请求，分别在 1440px 深色英文和 375px 浅色中文下截图，并在提交前检查图片元数据与内容。

## 快速开始

### Windows 发布包

解压 `WebhookStudio-v0.1.0-win-x64.zip`，双击 `WebhookStudio.exe`，然后打开控制台显示的 `http://127.0.0.1:8080`。也可运行：

```powershell
./WebhookStudio.exe --open-browser
```

发布包已包含 React 页面和 .NET Runtime，不需要 Node.js、Vite 或另一个后端进程。数据默认位于 `%LOCALAPPDATA%\WebhookStudio`，移动程序目录不会移动用户数据。

### Docker Compose

```powershell
docker compose up --build -d
```

打开 `http://127.0.0.1:8080`。Compose 只映射 loopback，并用命名卷保存 `/data`。

### 源码开发

需要 .NET SDK 8、Node.js 20 和 npm：

```powershell
dotnet restore WebhookStudio.sln --configfile NuGet.Config
cd src/WebhookStudio.Web
npm ci
cd ../..
./scripts/dev.ps1
```

开发模式使用 `http://localhost:5173` 的 Vite 和 `http://localhost:5080` 的 API；这只用于热更新。正式构建始终由 ASP.NET Core 托管 React。

## 捕获示例

创建 slug 为 `demo` 的 Endpoint：

```powershell
curl.exe -X POST "http://127.0.0.1:8080/hooks/demo/orders?source=example" -H "Content-Type: application/json" -d '{"orderId":42,"status":"paid"}'
```

## 配置

环境变量使用双下划线，例如 `WebhookStudio__MaxBodyBytes=2097152`。

| 配置 | 默认值 | 范围/说明 |
|---|---:|---|
| `ASPNETCORE_URLS` | `http://127.0.0.1:8080` | 非 loopback 绑定会扩大访问面 |
| `ConnectionStrings__Studio` | 操作系统用户数据目录 | SQLite 连接字符串 |
| `WebhookStudio__MaxBodyBytes` | 1048576 | 1024–10485760 |
| `WebhookStudio__DefaultRetentionLimit` | 500 | 10–10000 |
| `WebhookStudio__MaxHeaderCount` | 100 | 1–200 |
| `WebhookStudio__MaxHeaderValueLength` | 8192 | 128–32768 |
| `WebhookStudio__MaxPathLength` | 4096 | 128–16384 |
| `WebhookStudio__AllowPrivateNetworkReplay` | false | 开启后 UI 持续警告 |
| `WebhookStudio__MaxReplayRedirects` | 5 | 0–10 |
| `WebhookStudio__ReplayTimeoutSeconds` | 15 | 1–120 |
| `WebhookStudio__MaxReplayResponseBytes` | 1048576 | 1024–10485760 |

健康检查：`/health/live` 只表示进程存活，`/health/ready` 检查数据库。

## 安全模型与限制

重放仅允许无用户名密码的 HTTP/HTTPS URL；默认阻止 loopback、私网、link-local、multicast、unspecified、IPv4-mapped IPv6、DNS 解析到受限地址以及重定向到受限地址。连接时会再次解析和校验。启用私网重放只应用于可信本地开发，仍保留超时、重定向和响应上限。

React 将 Body 和 Header 作为文本渲染。导出和重放默认删除 Authorization、Cookie 等敏感 Header。应用不会记录捕获 Body 或 Header。SQLite 启用 WAL、外键和 busy timeout，但不适合高并发、多实例生产服务。

备份前停止应用，然后复制数据库及同目录的 `-wal`/`-shm` 文件；更稳妥的方式是停止后复制整个数据目录。恢复时先停止应用，保留原目录备份，再替换数据库文件。迁移只保证向前升级，不支持自动降级。

## 测试、架构与路线图

```powershell
dotnet test WebhookStudio.sln
cd src/WebhookStudio.Web
npm run typecheck
npm test
npm run build
cd ../..
./scripts/test-e2e.ps1 -NoPause
```

架构见 [docs/architecture.md](docs/architecture.md)，贡献见 [CONTRIBUTING.md](CONTRIBUTING.md)，安全报告见 [SECURITY.md](SECURITY.md)。0.1.x 属于 pre-1.0：公开 API 和导入格式的破坏性变化会在变更日志中说明。路线图只包含安全修复、可访问性和发布可靠性改进；账号、云隧道、多租户和脚本执行不在当前范围。

许可证：[MIT](LICENSE)。
