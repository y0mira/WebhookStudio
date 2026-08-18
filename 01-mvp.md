# 第一阶段提示词：构建可运行 MVP

你是一名资深全栈工程师。请在当前空仓库中实现 `Webhook Studio` 的第一阶段 MVP。你可以创建和修改文件、安装依赖、运行命令与测试。不要只给建议或代码片段；持续工作到验收标准满足，或明确报告无法继续的外部阻塞。

## 1. 产品目标

Webhook Studio 是本地优先、自托管的 Webhook 调试工具。开发者创建一个接收端点，将第三方 Webhook 发往该地址，然后在浏览器中实时查看请求详情，并可将原请求重放到指定 URL。

本阶段只完成这条闭环：

```text
创建端点 -> 接收 HTTP 请求 -> SQLite 保存 -> 实时出现在页面 -> 查看详情 -> 重放到目标 URL
```

## 2. 技术栈与约束

- 使用 .NET 8、ASP.NET Core Minimal API、EF Core SQLite、SignalR。
- 前端使用 React、TypeScript、Vite、Tailwind CSS、TanStack Query、React Router。
- 后端与前端放在同一个仓库，建议目录为 `src/WebhookStudio.Api`、`src/WebhookStudio.Web`、`tests/WebhookStudio.Api.Tests`。
- 开发时 Vite 独立运行；生产构建由 ASP.NET Core 托管前端静态文件。
- 使用单个 SQLite 数据库，不引入 Redis、消息队列、身份系统、微服务或云服务。
- 不实现多租户、团队、登录、插件系统、定时任务、云同步、国际化和复杂编辑器。
- 优先使用框架原生能力。不要为只有一个实现的功能创建多层抽象。
- 所有时间以 UTC 保存，API 返回 ISO 8601。
- 请求体第一版最大 1 MiB；超限返回 `413`，不得把超限内容写入数据库。
- 捕获端点不能记录 ASP.NET Core 添加的内部请求头，只保存客户端实际发送内容。

## 3. 数据模型

保持模型简单，至少包含：

### Endpoint

- `Id`: UUID
- `Name`: 1-80 个字符
- `Slug`: URL 安全、唯一、创建后不可修改
- `CreatedAtUtc`

### CapturedRequest

- `Id`: UUID
- `EndpointId`
- `Method`
- `PathAndQuery`
- `HeadersJson`
- `Body`: 原始字节
- `ContentType`
- `RemoteIp`
- `ReceivedAtUtc`
- `BodySize`

### ReplayAttempt

- `Id`: UUID
- `CapturedRequestId`
- `TargetUrl`
- `StatusCode`: 可空
- `DurationMs`
- `Succeeded`
- `Error`: 可空且限制长度
- `CreatedAtUtc`

不要保存 Cookie、授权令牌之外的额外推断信息。Headers 必须能保留一键重放所需的多值结构。

## 4. 后端功能

实现并用 OpenAPI 暴露以下能力：

- 创建、列出、查看、删除 Endpoint。
- `ANY /hooks/{slug}/{**remainingPath}` 接收任意常见 HTTP 方法，并保留剩余路径和查询字符串。
- 捕获成功默认返回 `200` 和简短 JSON，其中包含请求 ID。
- 分页列出某 Endpoint 的请求，默认按最新优先。
- 查看单个请求完整详情。
- 删除单个请求。
- 将捕获请求重放到用户提供的 `http` 或 `https` URL。
- 重放应复制原方法、Body 和安全的内容相关 Header；必须移除 `Host`、`Content-Length`、连接级 Header，并由 HttpClient 重新计算。
- 使用 HttpClientFactory，并设置合理超时。
- SignalR 以 Endpoint 为分组，新请求写入数据库成功后广播请求摘要。
- 数据库启动时自动创建；开发阶段可使用 EF Core migration 或 `EnsureCreated`，二选一并在 README 中说明。
- 提供 `/health`。

### 本阶段 SSRF 边界

这是仅面向本机开发环境的 MVP。重放功能只接受合法 `http/https` 绝对地址，并设置超时；在 UI 和 README 明确标注“不要暴露到公网”。完整私网地址阻断留到第三阶段，但代码结构不要妨碍后续加入校验。

## 5. 前端功能

实现三个主要页面/状态：

1. Endpoint 列表：创建、复制接收 URL、删除、进入详情。
2. Endpoint 工作台：左侧请求列表，右侧请求详情；新请求通过 SignalR 实时插入，不刷新页面。
3. 请求详情：展示方法、时间、路径、查询、Headers 和 Body；JSON Body 格式化，非 JSON 显示纯文本或“二进制内容”提示。

请求详情至少提供：

- 复制完整接收信息为文本。
- 复制为 curl。
- 输入目标 URL 并执行重放。
- 清楚显示重放中的状态、成功状态码、耗时或错误。

界面本阶段追求清晰和可用，不投入复杂视觉效果。必须具备：

- 桌面端双栏布局；小屏切为列表/详情顺序布局。
- 清晰的空状态、加载状态和错误状态。
- 所有表单有可见标签；按钮可以键盘操作；焦点轮廓不可移除。
- 不用 emoji 充当图标。

## 6. API 与错误约定

- 使用 Problem Details 返回 API 错误。
- 验证错误返回 `400`；不存在返回 `404`；slug 冲突返回 `409`；Body 超限返回 `413`。
- 前端不得只显示“Something went wrong”，应展示可操作的中文或英文具体错误。
- 列表 API 使用稳定分页参数，例如 `page`、`pageSize`，并限制最大 pageSize。

## 7. 测试要求

至少实现并运行：

- 创建 Endpoint 成功、slug 冲突和非法输入测试。
- 捕获 GET 查询参数和 POST JSON 的集成测试。
- 大于 1 MiB 请求返回 413 的测试。
- 请求列表按时间倒序和分页测试。
- 重放可保留方法、Body、Content-Type，并去除 Host 的集成测试；测试中使用本地 TestServer，不访问公网。
- 前端至少覆盖创建 Endpoint、空状态、选择请求、重放反馈四个关键组件/流程测试。
- 一条 Playwright 冒烟测试：创建 Endpoint 后，模拟发送请求，并在 UI 中看到该请求。

不要为了测试而修改生产行为。测试需可重复运行，不依赖网络、当前时区或执行顺序。

## 8. 文档和开发体验

创建根目录 README，包含：

- 产品介绍和当前 MVP 边界。
- 环境要求。
- 后端、前端和测试的准确启动命令。
- curl 捕获示例。
- 数据库文件位置和清理方式。
- “仅用于本地开发，不要直接暴露公网”的安全提示。

提供 `.gitignore`、`.editorconfig` 和方便开发的一条启动方式。若使用脚本，应兼容 PowerShell；不要要求安装全局 npm 包。

## 9. 执行顺序

1. 检查当前目录，声明关键假设和简短实现计划。
2. 搭建最小项目结构并确保后端、前端分别能启动。
3. 先完成数据模型和 API，再完成 SignalR 和前端。
4. 添加测试并运行与改动匹配的检查。
5. 修复失败，直到测试通过。
6. 最后检查 `git diff`，确认没有生成物、密钥、数据库或无关文件被纳入。

## 10. 完成标准

只有同时满足以下条件才算完成：

- 新用户按 README 能在本机启动项目。
- curl 发到接收 URL 后，页面无需刷新即可显示请求。
- 请求详情能正确显示 JSON、Headers、路径和查询。
- 重放能被本地测试服务收到，结果有明确反馈。
- 后端和前端测试全部通过。
- 没有伪实现、TODO 占位、硬编码演示数据或未使用的大型依赖。

完成后输出：实现摘要、项目结构、实际执行过的命令及结果、已知限制、下一阶段建议。不要声称没有运行过的测试通过。
