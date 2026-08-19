# Release checklist

- [ ] Clean checkout restore, build, unit tests, and Playwright pass.
- [ ] Docker image and Compose health/persistence verified on amd64; arm64 image builds.
- [ ] Five self-contained archives and SHA-256 files generated; each includes `wwwroot/index.html`.
- [ ] Windows x64 archive starts without Vite, persists data, survives directory move, reads its port from `appsettings.json`, and opens the browser automatically after a double-click-style launch.
- [ ] SSRF, capture limits, malicious text rendering, and private-network opt-in tests pass.
- [ ] English and Chinese key parity, persistence, core E2E, accessibility names, and four viewport widths pass.
- [ ] README languages match; changelog and release notes are current.
- [ ] Git contains no database, logs, secrets, test output, or build artifacts.
