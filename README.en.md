# Webhook Studio

English | [简体中文](README.md)

Webhook Studio is a local-first, single-process open-source webhook debugger. Receive, inspect, filter, compare, replay, import, and export requests without sending data to a third party.

> This project has no authentication and listens only on `127.0.0.1` by default. Captures and databases may contain secrets. Protect them as sensitive data and do not expose the service directly to the public internet.

## Features

- Local endpoints, SQLite persistence, and a live SignalR request stream
- Server-side method, status, time, path, query, and displayable body filters
- Safe header, JSON, text, and binary inspection with structural JSON comparison
- Configurable responses, asynchronous delay, retention, curl, HAR, and versioned JSON
- Sensitive-header redaction and private/local replay blocking by default
- Light/dark themes, instant English/Simplified Chinese switching, and 375px-to-desktop layouts

Screenshots are not committed yet. To capture them, run the production single process, create two sanitized sample requests, capture a 1440px dark English view and a 375px light Chinese view, then inspect image metadata and content before committing.

## Quick start

### Windows release archive

Extract `WebhookStudio-v0.1.0-win-x64.zip`, double-click `WebhookStudio.exe`, and open the displayed `http://127.0.0.1:8080`. Or run:

```powershell
./WebhookStudio.exe --open-browser
```

The archive contains the React application and .NET runtime. It does not need Node.js, Vite, or a second backend process. Data defaults to `%LOCALAPPDATA%\WebhookStudio`, so moving the program does not move user data.

### Docker Compose

```powershell
docker compose up --build -d
```

Open `http://127.0.0.1:8080`. Compose binds only to loopback and persists `/data` in a named volume.

### Source development

Install .NET SDK 8, Node.js 20, and npm:

```powershell
dotnet restore WebhookStudio.sln --configfile NuGet.Config
cd src/WebhookStudio.Web
npm ci
cd ../..
./scripts/dev.ps1
```

Development uses Vite at `http://localhost:5173` and the API at `http://localhost:5080` for hot reload only. Production always serves React from ASP.NET Core.

## Capture example

Create an endpoint with slug `demo`:

```powershell
curl.exe -X POST "http://127.0.0.1:8080/hooks/demo/orders?source=example" -H "Content-Type: application/json" -d '{"orderId":42,"status":"paid"}'
```

## Configuration

Environment variables use double underscores, for example `WebhookStudio__MaxBodyBytes=2097152`.

| Setting | Default | Range/notes |
|---|---:|---|
| `ASPNETCORE_URLS` | `http://127.0.0.1:8080` | Non-loopback binding expands exposure |
| `ConnectionStrings__Studio` | OS user data directory | SQLite connection string |
| `WebhookStudio__MaxBodyBytes` | 1048576 | 1024–10485760 |
| `WebhookStudio__DefaultRetentionLimit` | 500 | 10–10000 |
| `WebhookStudio__MaxHeaderCount` | 100 | 1–200 |
| `WebhookStudio__MaxHeaderValueLength` | 8192 | 128–32768 |
| `WebhookStudio__MaxPathLength` | 4096 | 128–16384 |
| `WebhookStudio__AllowPrivateNetworkReplay` | false | UI shows a persistent warning when enabled |
| `WebhookStudio__MaxReplayRedirects` | 5 | 0–10 |
| `WebhookStudio__ReplayTimeoutSeconds` | 15 | 1–120 |
| `WebhookStudio__MaxReplayResponseBytes` | 1048576 | 1024–10485760 |

`/health/live` checks the process only; `/health/ready` checks database connectivity.

## Security model and limitations

Replay permits only credential-free HTTP/HTTPS URLs. By default it blocks loopback, private, link-local, multicast, unspecified, IPv4-mapped IPv6, DNS results in restricted ranges, and redirects to restricted ranges. DNS is resolved and checked again at connection time. Private replay opt-in is for trusted local development only; timeout, redirect, and response limits remain active.

React renders bodies and headers as text. Export and replay remove Authorization, Cookie, and related headers by default. The application does not log captured bodies or headers. SQLite uses WAL, foreign keys, and a busy timeout, but is not intended for high-concurrency or multi-instance production service.

For backup, stop the application and copy the database plus adjacent `-wal`/`-shm` files; copying the entire stopped data directory is safer. For restore, stop the application, preserve the original directory, and replace the files. Migrations support forward upgrades only; automatic downgrade is not supported.

## Tests, architecture, and roadmap

```powershell
dotnet test WebhookStudio.sln
cd src/WebhookStudio.Web
npm run typecheck
npm test
npm run build
cd ../..
./scripts/test-e2e.ps1 -NoPause
```

See [docs/architecture.md](docs/architecture.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [SECURITY.md](SECURITY.md). Version 0.1.x is pre-1.0: breaking public API or import-format changes are documented in the changelog. The roadmap is limited to security, accessibility, and release reliability; accounts, cloud tunnels, multi-tenancy, and script execution are out of scope.

License: [MIT](LICENSE).
