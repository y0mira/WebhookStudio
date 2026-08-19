# Architecture

Webhook Studio is one ASP.NET Core process in production. It serves the compiled React assets, Minimal API routes, webhook capture routes, and a SignalR hub. Development may run Vite separately with a proxy to the API.

```text
Browser -> ASP.NET Core static files / Minimal API / SignalR
Webhook sender -> /hooks/{slug}/... -> EF Core -> SQLite
Replay request -> replay policy -> DNS/IP validation -> bounded HTTP client
```

EF Core migrations create and evolve SQLite. Startup enables WAL, foreign keys, and a busy timeout. SQLite is appropriate for a single local instance, not a high-concurrency distributed service. Captures are inserted transactionally and old rows are trimmed by the endpoint retention limit. SignalR is notification-only; reconnect causes a normal paged query so missed events are recovered.

Replay disables automatic redirects and validates every redirect. DNS is validated before sending and again in the socket connection callback. React renders captured content as text; no captured HTML is inserted into the DOM. Import/export format major version is `1`; unknown versions are rejected.

Configuration comes from `appsettings.json`, environment variables, and command-line hosting settings. Production defaults to loopback and the operating-system user application-data directory.
