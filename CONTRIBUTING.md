# Contributing

Thanks for improving Webhook Studio. Install .NET SDK 8, Node.js 20, npm, and PowerShell. Run `dotnet restore WebhookStudio.sln --configfile NuGet.Config`, then `npm ci` in `src/WebhookStudio.Web`.

For development, start the API and Vite separately as documented in README. Before a pull request run:

```powershell
dotnet format WebhookStudio.sln --verify-no-changes
dotnet test WebhookStudio.sln
cd src/WebhookStudio.Web
npm run format:check
npm run typecheck
npm test
npm run build
cd ../..
./scripts/test-e2e.ps1 -NoPause
```

Keep changes scoped, add tests for behavior, never commit captured secrets or databases, and update both README languages when behavior changes. PRs should explain motivation, tests, security impact, and UI screenshots when applicable. Commits should be small and descriptive; Conventional Commit prefixes are welcome but not required.
