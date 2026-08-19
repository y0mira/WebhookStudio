using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebhookStudio.Api;

const int maxPageSize = 100;
var packagedContentRoot = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")) ? AppContext.BaseDirectory : Directory.GetCurrentDirectory();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = packagedContentRoot });
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var configuredUrls = builder.Configuration["urls"] ?? builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["Hosting:Url"] ?? "http://127.0.0.1:8090";
var openBrowserOnStart = builder.Configuration.GetValue("Hosting:OpenBrowserOnStart", true);
builder.WebHost.UseUrls(configuredUrls);
builder.Services.AddOptions<StudioOptions>().Bind(builder.Configuration.GetSection(StudioOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
var configuredConnection = builder.Configuration.GetConnectionString("Studio");
var dataDirectory = "configured by ConnectionStrings:Studio";
if (string.IsNullOrWhiteSpace(configuredConnection)) { dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); if (string.IsNullOrWhiteSpace(dataDirectory)) dataDirectory = AppContext.BaseDirectory; dataDirectory = Path.Combine(dataDirectory, "WebhookStudio"); Directory.CreateDirectory(dataDirectory); }
var connection = configuredConnection ?? $"Data Source={Path.Combine(dataDirectory, "webhook-studio.db")};Foreign Keys=True;Default Timeout=5";
builder.Services.AddDbContext<StudioDbContext>(o => o.UseSqlite(connection));
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDnsResolver, SystemDnsResolver>();
builder.Services.AddSingleton<ReplaySecurity>();
builder.Services.AddHttpClient("replay").ConfigurePrimaryHttpMessageHandler(ReplayHttpHandler.Create);
builder.Services.AddScoped<ReplayService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 100) correlationId = context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId })) await next();
});
app.Use(async (context, next) => { context.Response.Headers["X-Content-Type-Options"] = "nosniff"; context.Response.Headers["X-Frame-Options"] = "DENY"; context.Response.Headers["Referrer-Policy"] = "no-referrer"; context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; connect-src 'self' ws: wss:; style-src 'self' 'unsafe-inline'; font-src 'self' data:; img-src 'self' data:"; await next(); });
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
}
var api = app.MapGroup("/api");

api.MapPost("/endpoints", async Task<Results<Created<EndpointResponse>, ValidationProblem, Conflict<ProblemDetails>>> (CreateEndpointRequest input, StudioDbContext db, IOptions<StudioOptions> options) =>
{
    var errors = ValidateEndpoint(input);
    if (errors.Count > 0) return TypedResults.ValidationProblem(errors, extensions: new Dictionary<string, object?> { { "code", "endpoint_invalid" } });
    var slug = input.Slug.Trim().ToLowerInvariant();
    if (await db.Endpoints.AnyAsync(x => x.Slug == slug))
        return TypedResults.Conflict(new ProblemDetails { Title = "Slug already exists", Detail = "Choose a different endpoint slug.", Extensions = { { "code", "endpoint_slug_conflict" } } });
    var endpoint = new WebhookStudio.Api.Endpoint { Id = Guid.NewGuid(), Name = input.Name.Trim(), Slug = slug, CreatedAtUtc = DateTime.UtcNow, RetentionLimit = options.Value.DefaultRetentionLimit };
    db.Endpoints.Add(endpoint);
    await db.SaveChangesAsync();
    return TypedResults.Created($"/api/endpoints/{endpoint.Id}", endpoint.ToResponse());
}).WithOpenApi();
api.MapGet("/endpoints", async (StudioDbContext db) => await db.Endpoints.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Select(x => x.ToResponse()).ToListAsync()).WithOpenApi();
api.MapGet("/endpoints/{id:guid}", async Task<Results<Ok<EndpointResponse>, NotFound>> (Guid id, StudioDbContext db) =>
{
    var endpoint = await db.Endpoints.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    return endpoint is null ? TypedResults.NotFound() : TypedResults.Ok(endpoint.ToResponse());
}).WithOpenApi();
api.MapDelete("/endpoints/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, StudioDbContext db) =>
{
    var endpoint = await db.Endpoints.FindAsync(id);
    if (endpoint is null) return TypedResults.NotFound();
    db.Endpoints.Remove(endpoint); await db.SaveChangesAsync(); return TypedResults.NoContent();
}).WithOpenApi();
api.MapPut("/endpoints/{id:guid}/settings", async (Guid id, EndpointSettingsRequest input, StudioDbContext db) =>
{
    var errors = ValidateSettings(input); if (errors.Count > 0) return Results.ValidationProblem(errors, extensions: new Dictionary<string, object?> { ["code"] = "settings_invalid" });
    var endpoint = await db.Endpoints.FindAsync(id); if (endpoint is null) return Results.NotFound();
    endpoint.ResponseStatusCode = input.ResponseStatusCode; endpoint.ResponseContentType = input.ResponseContentType.Trim(); endpoint.ResponseBody = input.ResponseBody;
    endpoint.ResponseDelayMs = input.ResponseDelayMs; endpoint.RetentionLimit = input.RetentionLimit; await db.SaveChangesAsync(); return Results.Ok(endpoint.ToResponse());
}).WithOpenApi();
api.MapDelete("/endpoints/{id:guid}/requests", async (Guid id, StudioDbContext db) =>
{
    if (!await db.Endpoints.AnyAsync(x => x.Id == id)) return Results.NotFound(); var count = await db.CapturedRequests.Where(x => x.EndpointId == id).ExecuteDeleteAsync(); return Results.Ok(new { deleted = count });
}).WithOpenApi();
api.MapGet("/endpoints/{id:guid}/requests", async Task<Results<Ok<PagedResponse<RequestSummary>>, NotFound, ValidationProblem>> (Guid id, int? page, int? pageSize, string? method, int? statusCategory, DateTime? from, DateTime? to, string? search, StudioDbContext db) =>
{
    var p = page ?? 1; var size = pageSize ?? 25;
    if (p < 1 || size < 1 || size > maxPageSize)
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["pagination"] = ["page must be at least 1 and pageSize must be between 1 and 100."] });
    if (!await db.Endpoints.AnyAsync(x => x.Id == id)) return TypedResults.NotFound();
    var query = db.CapturedRequests.AsNoTracking().Where(x => x.EndpointId == id);
    if (!string.IsNullOrWhiteSpace(method)) query = query.Where(x => x.Method == method.ToUpper());
    if (statusCategory is >= 1 and <= 5) query = query.Where(x => x.ResponseStatusCode >= statusCategory * 100 && x.ResponseStatusCode < (statusCategory + 1) * 100);
    if (from.HasValue) query = query.Where(x => x.ReceivedAtUtc >= from.Value.ToUniversalTime()); if (to.HasValue) query = query.Where(x => x.ReceivedAtUtc <= to.Value.ToUniversalTime());
    if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => EF.Functions.Like(x.PathAndQuery, $"%{term}%") || (x.BodyText != null && EF.Functions.Like(x.BodyText, $"%{term}%"))); }
    var total = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.ReceivedAtUtc).ThenByDescending(x => x.Id).Skip((p - 1) * size).Take(size).Select(x => x.ToSummary()).ToListAsync();
    return TypedResults.Ok(new PagedResponse<RequestSummary>(items, p, size, total));
}).WithOpenApi();
api.MapPost("/endpoints/{id:guid}/compare", async (Guid id, CompareRequest input, StudioDbContext db) =>
{
    var pair = await db.CapturedRequests.AsNoTracking().Where(x => x.EndpointId == id && (x.Id == input.LeftId || x.Id == input.RightId)).ToListAsync();
    var left = pair.SingleOrDefault(x => x.Id == input.LeftId); var right = pair.SingleOrDefault(x => x.Id == input.RightId); if (left is null || right is null) return Results.NotFound();
    return Results.Ok(new CompareResponse(PhaseTwo.Compare(left, right)));
}).WithOpenApi();
api.MapGet("/endpoints/{id:guid}/export", async (Guid id, bool? includeSensitive, StudioDbContext db) =>
{
    var endpoint = await db.Endpoints.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id); if (endpoint is null) return Results.NotFound();
    var rows = await db.CapturedRequests.AsNoTracking().Where(x => x.EndpointId == id).OrderBy(x => x.ReceivedAtUtc).Take(10000).ToListAsync();
    if (rows.Sum(x => x.BodySize) > 20 * 1024 * 1024) return Results.Problem(statusCode: 413, title: "Export too large", extensions: new Dictionary<string, object?> { ["code"] = "export_too_large" });
    var items = rows.Select(x => new ExportedRequest(x.Id, x.Method, x.PathAndQuery, PhaseTwo.SafeHeaders(x.HeadersJson, includeSensitive == true), Convert.ToBase64String(x.Body), x.ContentType, x.ReceivedAtUtc, x.ResponseStatusCode)).ToList();
    return Results.File(JsonSerializer.SerializeToUtf8Bytes(new ExportPackage(1, endpoint.ToResponse(), items)), "application/json", $"webhook-studio-{endpoint.Slug}.json");
}).WithOpenApi();
api.MapPost("/endpoints/{id:guid}/import", async (Guid id, HttpRequest request, StudioDbContext db, IOptions<StudioOptions> configured) =>
{
    if (request.ContentLength > 5 * 1024 * 1024) return Results.Problem(statusCode: 413, title: "Import too large", detail: "Import packages are limited to 5 MiB.", extensions: new Dictionary<string, object?> { ["code"] = "import_too_large" });
    var endpoint = await db.Endpoints.FindAsync(id); if (endpoint is null) return Results.NotFound(); ExportPackage? package;
    try { package = await request.ReadFromJsonAsync<ExportPackage>(); } catch (JsonException) { return Results.BadRequest(new ProblemDetails { Title = "Invalid import package", Detail = "The file is not valid Webhook Studio JSON.", Extensions = { ["code"] = "import_invalid" } }); }
    if (package is null || package.Version != 1 || package.Requests.Count > 10000) return Results.BadRequest(new ProblemDetails { Title = "Invalid import package", Detail = "Expected a version 1 package with at most 10000 requests.", Extensions = { ["code"] = "import_invalid" } });
    var imported = 0; foreach (var x in package.Requests) { if (!Regex.IsMatch(x.Method, "^[A-Z]{1,16}$") || x.PathAndQuery.Length > configured.Value.MaxPathLength || x.Headers.Count > configured.Value.MaxHeaderCount || x.Headers.Any(h => h.Key.Length > 256 || h.Value.Sum(v => v.Length) > configured.Value.MaxHeaderValueLength)) return Results.BadRequest(new ProblemDetails { Title = "Invalid import package", Detail = "A request contains invalid or oversized metadata.", Extensions = { ["code"] = "import_invalid" } }); byte[] body; try { body = Convert.FromBase64String(x.BodyBase64); } catch { return Results.BadRequest(new ProblemDetails { Title = "Invalid import package", Detail = "A request body is not valid base64.", Extensions = { ["code"] = "import_invalid" } }); } if (body.Length > configured.Value.MaxBodyBytes) return Results.BadRequest(new ProblemDetails { Title = "Invalid import package", Detail = "A request body exceeds the configured limit.", Extensions = { ["code"] = "import_invalid" } }); db.CapturedRequests.Add(new CapturedRequest { Id = Guid.NewGuid(), EndpointId = id, Method = x.Method, PathAndQuery = x.PathAndQuery, HeadersJson = JsonSerializer.Serialize(x.Headers), Body = body, BodySize = body.Length, BodyText = PhaseTwo.DisplayText(body, x.ContentType), ContentType = x.ContentType, ReceivedAtUtc = x.ReceivedAtUtc, ResponseStatusCode = x.ResponseStatusCode }); imported++; }
    await db.SaveChangesAsync(); return Results.Ok(new ImportResult(imported, 0));
}).WithOpenApi();
api.MapGet("/requests/{id:guid}", async Task<Results<Ok<RequestDetail>, NotFound>> (Guid id, StudioDbContext db) =>
{
    var request = await db.CapturedRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    return request is null ? TypedResults.NotFound() : TypedResults.Ok(request.ToDetail());
}).WithOpenApi();
api.MapDelete("/requests/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, StudioDbContext db) =>
{
    var request = await db.CapturedRequests.FindAsync(id);
    if (request is null) return TypedResults.NotFound();
    db.CapturedRequests.Remove(request); await db.SaveChangesAsync(); return TypedResults.NoContent();
}).WithOpenApi();
api.MapGet("/requests/{id:guid}/export", async (Guid id, string format, bool? includeSensitive, StudioDbContext db) =>
{
    var x = await db.CapturedRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id); if (x is null) return Results.NotFound(); var headers = PhaseTwo.SafeHeaders(x.HeadersJson, includeSensitive == true);
    if (format.Equals("curl", StringComparison.OrdinalIgnoreCase)) { var hs = string.Join(" ", headers.Where(h => !h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)).Select(h => $"-H '{h.Key}: {string.Join(", ", h.Value)}'")); var body = x.BodyText is { Length: > 0 } ? $" --data-raw '{x.BodyText.Replace("'", "'\\''")}'" : ""; return Results.File(Encoding.UTF8.GetBytes($"curl -X {x.Method} {hs}{body} 'TARGET_URL'"), "text/plain; charset=utf-8", $"request-{id}.curl.txt"); }
    if (format.Equals("har", StringComparison.OrdinalIgnoreCase)) { var har = new { startedDateTime = x.ReceivedAtUtc.ToString("O"), request = new { method = x.Method, url = x.PathAndQuery, httpVersion = "HTTP/1.1", headers = headers.SelectMany(h => h.Value.Select(v => new { name = h.Key, value = v })), queryString = Array.Empty<object>(), postData = x.BodyText is null ? null : new { mimeType = x.ContentType, text = x.BodyText }, headersSize = -1, bodySize = x.BodySize }, response = new { status = x.ResponseStatusCode, statusText = "", httpVersion = "HTTP/1.1", headers = Array.Empty<object>(), content = new { size = 0, mimeType = "text/plain" }, redirectURL = "", headersSize = -1, bodySize = 0 }, cache = new { }, timings = new { send = 0, wait = 0, receive = 0 } }; return Results.File(JsonSerializer.SerializeToUtf8Bytes(har), "application/json", $"request-{id}.har"); }
    return Results.ValidationProblem(new Dictionary<string, string[]> { { "format", ["Use curl or har."] } });
}).WithOpenApi();
api.MapPost("/requests/{id:guid}/replay", async Task<Results<Ok<ReplayResponse>, ValidationProblem, NotFound>> (Guid id, ReplayRequest input, StudioDbContext db, ReplayService replay, CancellationToken token) =>
{
    if (!Uri.TryCreate(input.TargetUrl, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https"))
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["targetUrl"] = ["Enter an absolute http or https URL."] }, extensions: new Dictionary<string, object?> { ["code"] = "replay_url_invalid" });
    var captured = await db.CapturedRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
    if (captured is null) return TypedResults.NotFound();
    var result = await replay.ReplayAsync(captured, target, token);
    var attempt = new ReplayAttempt { Id = Guid.NewGuid(), CapturedRequestId = id, TargetUrl = target.ToString(), StatusCode = result.StatusCode, DurationMs = result.DurationMs, Succeeded = result.Succeeded, Error = result.Error, CreatedAtUtc = DateTime.UtcNow };
    db.ReplayAttempts.Add(attempt); await db.SaveChangesAsync(token);
    return TypedResults.Ok(new ReplayResponse(attempt.Id, attempt.StatusCode, attempt.DurationMs, attempt.Succeeded, attempt.Error, result.Code, attempt.CreatedAtUtc));
}).WithOpenApi();

app.MapMethods("/hooks/{slug}/{**remainingPath}", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"], async (string slug, string? remainingPath, HttpContext context, StudioDbContext db, IHubContext<RequestHub> hub, IOptions<StudioOptions> configured) =>
{
    var limits = configured.Value; var maxBodyBytes = limits.MaxBodyBytes;
    var endpoint = await db.Endpoints.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower());
    if (endpoint is null) return Results.NotFound(new ProblemDetails { Title = "Endpoint not found" });
    if (context.Request.ContentLength > maxBodyBytes) return Results.Problem(statusCode: 413, title: "Request body too large", detail: "The maximum request body size is 1 MiB.");
    await using var body = new MemoryStream(); var buffer = new byte[81920]; int read;
    while ((read = await context.Request.Body.ReadAsync(buffer)) > 0)
    {
        if (body.Length + read > maxBodyBytes) return Results.Problem(statusCode: 413, title: "Request body too large", detail: "The maximum request body size is 1 MiB.");
        await body.WriteAsync(buffer.AsMemory(0, read));
    }
    if (context.Request.Path.Value?.Length > limits.MaxPathLength) return Results.Problem(statusCode: 414, title: "Request path too long", extensions: new Dictionary<string, object?> { { "code", "capture_path_too_long" } });
    if (context.Request.Headers.Count > limits.MaxHeaderCount || context.Request.Headers.Any(x => x.Value.Sum(v => v?.Length ?? 0) > limits.MaxHeaderValueLength)) return Results.Problem(statusCode: 431, title: "Request headers too large", extensions: new Dictionary<string, object?> { { "code", "capture_headers_too_large" } });
    var headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    var bytes = body.ToArray(); var captured = new CapturedRequest { Id = Guid.NewGuid(), EndpointId = endpoint.Id, Method = context.Request.Method, PathAndQuery = "/" + (remainingPath ?? "") + context.Request.QueryString, HeadersJson = JsonSerializer.Serialize(headers), Body = bytes, BodySize = body.Length, BodyText = PhaseTwo.DisplayText(bytes, context.Request.ContentType), ContentType = context.Request.ContentType, RemoteIp = context.Connection.RemoteIpAddress?.ToString(), ReceivedAtUtc = DateTime.UtcNow, ResponseStatusCode = endpoint.ResponseStatusCode };
    await using var transaction = await db.Database.BeginTransactionAsync(); db.CapturedRequests.Add(captured); await db.SaveChangesAsync();
    var excess = await db.CapturedRequests.Where(x => x.EndpointId == endpoint.Id).OrderByDescending(x => x.ReceivedAtUtc).ThenByDescending(x => x.Id).Skip(endpoint.RetentionLimit).Select(x => x.Id).ToListAsync(); if (excess.Count > 0) await db.CapturedRequests.Where(x => excess.Contains(x.Id)).ExecuteDeleteAsync(); await transaction.CommitAsync();
    await hub.Clients.Group(endpoint.Id.ToString()).SendAsync("RequestCaptured", captured.ToSummary());
    if (endpoint.ResponseDelayMs > 0) await Task.Delay(endpoint.ResponseDelayMs, context.RequestAborted); context.Response.ContentType = endpoint.ResponseContentType; context.Response.StatusCode = endpoint.ResponseStatusCode; return Results.Text(endpoint.ResponseBody, endpoint.ResponseContentType, statusCode: endpoint.ResponseStatusCode);
});
app.MapHub<RequestHub>("/hubs/requests");
var networkExposed = configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries).Any(x => Uri.TryCreate(x, UriKind.Absolute, out var u) && u.Host is not ("127.0.0.1" or "localhost" or "::1"));
if (networkExposed) app.Logger.LogWarning("Webhook Studio is bound to a non-loopback address. The application has no authentication.");
app.MapGet("/api/runtime", (IOptions<StudioOptions> options) => Results.Ok(new { allowPrivateNetworkReplay = options.Value.AllowPrivateNetworkReplay, networkExposed }));
app.MapGet("/health", () => Results.Redirect("/health/ready"));
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (StudioDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.Problem(statusCode: 503, title: "Database unavailable"));
app.MapFallbackToFile("index.html");
var publicUrl = configuredUrls.Split(';')[0];
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Webhook Studio 0.1.0\nManagement UI: {publicUrl}\nWebhook base: {publicUrl}/hooks/{{slug}}/\nDatabase: {dataDirectory}\nStop: Ctrl+C");
    if ((openBrowserOnStart || args.Contains("--open-browser", StringComparer.OrdinalIgnoreCase)) && Environment.UserInteractive && !app.Environment.IsEnvironment("Testing")) try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(publicUrl) { UseShellExecute = true }); } catch (Exception ex) { app.Logger.LogWarning("Could not open the browser: {Reason}", ex.Message); }
});
app.Run();

static Dictionary<string, string[]> ValidateEndpoint(CreateEndpointRequest input)
{
    var errors = new Dictionary<string, string[]>();
    var name = input.Name?.Trim() ?? ""; var slug = input.Slug?.Trim() ?? "";
    if (name.Length is < 1 or > 80) errors["name"] = ["Name must be between 1 and 80 characters."];
    if (!Regex.IsMatch(slug, "^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,78}[a-zA-Z0-9])?$")) errors["slug"] = ["Slug must be 1-80 letters, numbers, or hyphens and cannot begin or end with a hyphen."];
    return errors;
}
static Dictionary<string, string[]> ValidateSettings(EndpointSettingsRequest x) { var e = new Dictionary<string, string[]>(); if (x.ResponseStatusCode is < 100 or > 599) e["responseStatusCode"] = ["Status must be between 100 and 599."]; if (string.IsNullOrWhiteSpace(x.ResponseContentType) || x.ResponseContentType.Length > 200) e["responseContentType"] = ["Content-Type is required and limited to 200 characters."]; if (Encoding.UTF8.GetByteCount(x.ResponseBody ?? "") > 64 * 1024) e["responseBody"] = ["Response body is limited to 64 KiB."]; if (x.ResponseDelayMs is < 0 or > 10000) e["responseDelayMs"] = ["Delay must be between 0 and 10000 ms."]; if (x.RetentionLimit is < 10 or > 10000) e["retentionLimit"] = ["Retention must be between 10 and 10000."]; return e; }
public partial class Program;
