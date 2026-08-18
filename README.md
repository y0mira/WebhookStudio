# Webhook Studio

Webhook Studio is a local-first, self-hosted Webhook debugger. This MVP closes one workflow: create an endpoint, capture HTTP requests in SQLite, inspect them live in the browser, and replay the original request to a target URL.

> **Security warning:** this release is for local development only. Do not expose it directly to the public internet. Replay validates only that the target is an absolute `http` or `https` URL; private-network blocking is planned for a later phase.

## MVP scope

- Endpoint create/list/view/delete
- Any common HTTP method at `/hooks/{slug}/{remainingPath}`
- Raw body and multi-value header capture, limited to 1 MiB
- Latest-first paginated request history and SignalR live updates
- JSON/text/binary-aware request inspection, copy info, and copy curl
- Request replay with method, body, and content headers; connection-level headers are removed
- Problem Details API errors, OpenAPI at `/swagger`, and `/health`

Authentication, multi-tenancy, plugins, cloud sync, queues, and public-internet hardening are intentionally outside this MVP.

## Requirements

- .NET SDK 8
- Node.js 18 or newer and npm
- PowerShell 7 or Windows PowerShell for the convenience script

No global npm packages are required.

## Install and run

From the repository root:

```powershell
dotnet restore WebhookStudio.sln --configfile NuGet.Config
cd src/WebhookStudio.Web
npm install
cd ../..
./scripts/dev.ps1
```

The script starts the API on `http://localhost:5080` and Vite on `http://localhost:5173`. Open the Vite URL during development. Stop the Vite process with `Ctrl+C`; stop the background API process when finished.

To run the API and frontend separately:

```powershell
dotnet run --project src/WebhookStudio.Api --urls http://localhost:5080
```

```powershell
cd src/WebhookStudio.Web
npm run dev
```

## Capture a request

Create an endpoint with slug `demo`, then run:

```powershell
curl.exe -X POST "http://localhost:5080/hooks/demo/orders?source=example" -H "Content-Type: application/json" -d '{"orderId":42,"status":"paid"}'
```

The request appears in the open endpoint workspace without a page refresh.

## Production-style build

Vite writes its output into the API's `wwwroot`, so ASP.NET Core serves the SPA:

```powershell
cd src/WebhookStudio.Web
npm run build
cd ../..
dotnet run --project src/WebhookStudio.Api --urls http://localhost:5080
```

Then open `http://localhost:5080`.

## Tests

```powershell
dotnet test WebhookStudio.sln
cd src/WebhookStudio.Web
npm test
npx playwright install chromium
cd ../..
./scripts/test-e2e.ps1 -NoPause
```

The API tests use an in-memory SQLite database and a local ASP.NET Core `TestServer`; they never access the public internet. The Playwright test builds on the locally running production-style app.

## Database

The API uses EF Core `EnsureCreated` at startup. The SQLite files live under `src/WebhookStudio.Api/data/` and are ignored by Git. To reset local data, stop the API and remove that directory:

```powershell
Remove-Item -Recurse -Force -LiteralPath ./src/WebhookStudio.Api/data
```

All timestamps are stored as UTC and returned as ISO 8601 JSON values.

## Project layout

```text
src/WebhookStudio.Api/       Minimal API, EF Core SQLite, SignalR, replay service
src/WebhookStudio.Web/       React, TypeScript, Vite, Tailwind, Query, Router
tests/WebhookStudio.Api.Tests/ API and replay integration tests
scripts/dev.ps1              One-command local development launcher
```
