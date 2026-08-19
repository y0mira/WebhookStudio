# Webhook Studio 操作手册

## 1. 启动应用

### 1.1 Windows x64

1. 解压 `WebhookStudio-v0.1.0-win-x64.zip`，不要直接在压缩包预览窗口运行。
2. 双击 `WebhookStudio.exe`。
3. 服务成功监听后，默认浏览器会自动打开 `http://127.0.0.1:8090`。
4. 保持控制台窗口打开。关闭控制台或按 `Ctrl+C` 会停止服务。

发布包已经包含 React 页面和 .NET Runtime，不需要另外启动前端、安装 Node.js 或安装 .NET Runtime。

### 1.2 macOS Apple Silicon

`osx-arm64` 适用于 M1、M2、M3、M4 等 Apple 芯片 Mac，不适用于 Intel Mac。解压后在终端进入发布目录并执行：

```bash
chmod +x WebhookStudio
./WebhookStudio
```

如果 Gatekeeper 阻止未经签名的程序，可在“系统设置 → 隐私与安全性”中确认允许。仅在确认文件来自可信发布来源后，才考虑移除隔离属性：

```bash
xattr -d com.apple.quarantine WebhookStudio
```

### 1.3 Linux

根据处理器选择 `linux-x64` 或 `linux-arm64`，解压后运行：

```bash
chmod +x WebhookStudio
./WebhookStudio
```

无桌面环境时不会自动打开浏览器，请从同一台机器访问配置的地址，或在安全网络配置下使用浏览器连接。

### 1.4 Docker Compose

在项目根目录执行：

```powershell
docker compose up --build -d
```

打开 `http://127.0.0.1:8080`。查看日志和停止服务：

```powershell
docker compose logs -f
docker compose down
```

Compose 使用命名卷保存 `/data`。普通 `docker compose down` 不会删除该卷；不要在需要保留数据时使用 `down -v`。

## 2. 修改端口和自动打开行为

关闭 Webhook Studio，在可执行程序同目录打开 `appsettings.json`：

```json
{
  "Hosting": {
    "Url": "http://127.0.0.1:8090",
    "OpenBrowserOnStart": true
  }
}
```

- 修改 `8090` 即可更换端口，例如改为 `9000`。
- `OpenBrowserOnStart` 为 `true` 时，监听成功后自动打开浏览器。
- 改为 `false` 后只启动服务，不自动打开网页。
- 修改 JSON 时保留双引号、逗号和大括号，保存后重新启动程序。
- 建议继续使用 `127.0.0.1`。改成非 loopback 地址会让其他设备可能访问服务，而本项目没有身份认证。

若端口被占用或被 Windows 禁止，选择其他未使用端口并同步修改访问地址。命令行参数和环境变量可以临时覆盖文件配置：

```powershell
./WebhookStudio.exe --urls http://127.0.0.1:9000 --open-browser
```

## 3. 创建 Endpoint

1. 打开管理页面。
2. 在“创建 Endpoint”区域填写名称，例如“支付通知”。
3. 填写 Slug，例如 `payment-events`。
4. 点击“创建 Endpoint”。

Slug 会成为接收地址的一部分，只能使用字母、数字和连字符，不能以连字符开头或结尾。同一个应用中不能创建重复 Slug。

创建后页面会显示类似地址：

```text
http://127.0.0.1:8090/hooks/payment-events/
```

路径尾部可以继续添加任意子路径和查询参数。

## 4. 发送测试 Webhook

先创建 Slug 为 `demo` 的 Endpoint，然后在 PowerShell 执行：

```powershell
curl.exe -X POST "http://127.0.0.1:8090/hooks/demo/orders?source=manual" `
  -H "Content-Type: application/json" `
  -d '{"orderId":42,"status":"paid"}'
```

也可以使用 PowerShell 原生命令：

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri "http://127.0.0.1:8090/hooks/demo/orders?source=manual" `
  -ContentType "application/json" `
  -Body '{"orderId":42,"status":"paid"}'
```

发送成功后，新请求应实时出现在请求列表。如果没有出现，请确认：

- Webhook Studio 控制台仍在运行。
- URL 中的端口与 `appsettings.json` 一致。
- Slug 与页面中创建的 Slug 完全一致。
- 页面连接状态显示“已连接”。

## 5. 查看和筛选请求

点击请求列表中的一条记录，可以查看：

- HTTP Method、路径和 Query。
- Header。
- Body 内容和大小。
- 来源 IP、接收时间及响应状态。

Body 较长时可展开完整内容；二进制内容只按安全方式展示，不会作为 HTML 或脚本执行。

列表上方支持：

- 关键词：搜索路径、查询参数或可显示的 Body。
- HTTP Method。
- 响应状态类别。
- 开始和结束时间。
- 上一页和下一页。

筛选条件保存在当前 URL 中，刷新或切换中英文不会清除。

## 6. 比较两条请求

1. 在请求列表中点击第一条请求旁的“加入比较”。
2. 再选择第二条请求。
3. 页面自动进入“请求比较”视图。
4. 查看字段的“新增”“删除”或“变化”状态。
5. 点击“关闭比较”返回单条请求详情。

比较适合检查相同 Webhook 在不同时间、状态或版本下的 JSON、Header 和元数据差异。

## 7. 重放请求

1. 选择一条已捕获请求。
2. 在“重放请求”区域输入目标 HTTP 或 HTTPS URL。
3. 点击“重放”。
4. 查看返回的 HTTP 状态、耗时或错误原因。

安全限制：

- 默认禁止重放到 `localhost`、`127.0.0.1`、私网、链路本地地址和其他受限地址。
- 不支持非 HTTP/HTTPS 协议或包含用户名密码的 URL。
- 每次重定向都会重新进行目标校验。
- Authorization、Cookie 等敏感 Header 默认不会随重放发送。

只有在可信本地开发环境确实需要时，才可在 `appsettings.json` 的 `WebhookStudio` 节中启用：

```json
"AllowPrivateNetworkReplay": true
```

启用后重启应用，页面会持续显示私网重放风险警告。

## 8. 设置 Endpoint 响应

点击“Endpoint 设置”，可以调整：

- 状态码：100–599。
- Content-Type。
- 响应 Body。
- 延迟：0–10000 毫秒。
- 保留请求数：10–10000。

点击“保存设置”后，新收到的 Webhook 使用这些响应设置。修改设置不会改变已经捕获的旧请求。

## 9. 复制、导入和导出

在请求详情中可以：

- “复制信息”：复制当前请求的概览和内容。
- “复制 curl”：生成可编辑的 curl 命令。
- “HAR”：下载当前请求的 HAR 数据。

在 Endpoint 操作区可以：

- “导出 JSON”：导出当前 Endpoint 及最多 10000 条请求。
- “导入 JSON”：把兼容的 Webhook Studio 版本 1 导出包导入当前 Endpoint。
- “清空请求”：删除当前 Endpoint 的全部请求，此操作不可撤销。

导出文件可能包含敏感业务数据。应用默认对敏感 Header 脱敏，但仍应安全保存和传输文件。

## 10. 中英文和主题

- 点击顶栏“中文”或“English”即时切换语言。
- 语言选择保存在浏览器中，刷新和重启后保持。
- 切换语言不会清空当前页面、筛选、选中请求或未保存的设置表单。
- 主题按钮可以切换深色和浅色模式，选择同样保存在浏览器中。

## 11. 数据位置、备份和恢复

未配置自定义连接字符串时，Windows 数据库默认位于：

```text
%LOCALAPPDATA%\WebhookStudio\webhook-studio.db
```

备份建议：

1. 先关闭 Webhook Studio。
2. 复制整个 `WebhookStudio` 数据目录，而不只是单个数据库文件。
3. 将备份保存到受保护的位置。

恢复建议：

1. 关闭应用。
2. 先备份当前数据目录。
3. 用备份文件替换数据目录。
4. 启动应用并检查 Endpoint 和请求。

数据库迁移只支持向前升级，不提供自动降级。

## 12. 健康检查与停止

健康检查地址：

```text
/health/live
/health/ready
```

- `live` 表示进程正在运行。
- `ready` 额外检查数据库是否可连接。

正常停止方式：

- Windows、macOS、Linux 终端：按 `Ctrl+C`。
- Windows 双击启动：关闭控制台窗口。
- Docker Compose：执行 `docker compose down`。

## 13. 常见问题

### 双击 EXE 后提示 SocketException 10013

默认端口可能被 Windows 保留或安全软件阻止。关闭程序，编辑 EXE 同目录的 `appsettings.json`，把 `Hosting.Url` 改为其他端口，例如：

```json
"Url": "http://127.0.0.1:9000"
```

### 浏览器没有自动打开

确认 `Hosting.OpenBrowserOnStart` 为 `true`。也可以手动打开控制台显示的 Management UI 地址。

### 页面打开但看不到新请求

检查发送 URL 的端口、Slug 和路径，确认控制台没有启动失败信息，并查看页面 SignalR 状态是否为“已连接”。

### 无法重放到本机接口

这是默认 SSRF 防护，不是故障。仅在可信开发环境中评估风险后启用 `AllowPrivateNetworkReplay`。

### macOS 提示无法验证开发者

当前发布包没有 Apple 签名和公证。在确认文件来源及 SHA-256 后，通过系统“隐私与安全性”页面允许运行。macOS 包尚未在真实 Mac 上完成运行验收。

## 14. 使用限制

- 不包含身份认证，不要直接暴露到公网。
- 不提供公网 Webhook 隧道。
- 不提供账号、云同步或多人协作。
- SQLite 面向本地单实例，不适合高并发部署。
- 捕获内容和导出文件可能包含秘密，使用后应及时清理或妥善保管。

更多信息参见[项目介绍](project-introduction.md)、[架构说明](architecture.md)和项目根目录的 [SECURITY.md](../SECURITY.md)。
