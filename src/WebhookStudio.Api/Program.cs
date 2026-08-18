using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebhookStudio.Api;

const int maxBodyBytes = 1024 * 1024;
const int maxPageSize = 100;
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var connection = builder.Configuration.GetConnectionString("Studio") ?? "Data Source=data/webhook-studio.db";
if (connection.Contains("data/webhook-studio.db", StringComparison.OrdinalIgnoreCase))
    Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));
builder.Services.AddDbContext<StudioDbContext>(o => o.UseSqlite(connection));
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("replay", client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<ReplayService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<StudioDbContext>().Database.EnsureCreatedAsync();
var api = app.MapGroup("/api");

api.MapPost("/endpoints", async Task<Results<Created<EndpointResponse>, ValidationProblem, Conflict<ProblemDetails>>> (CreateEndpointRequest input, StudioDbContext db) =>
{
    var errors = ValidateEndpoint(input);
    if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
    var slug = input.Slug.Trim().ToLowerInvariant();
    if (await db.Endpoints.AnyAsync(x => x.Slug == slug))
        return TypedResults.Conflict(new ProblemDetails { Title = "Slug already exists", Detail = "Choose a different endpoint slug." });
    var endpoint = new WebhookStudio.Api.Endpoint { Id = Guid.NewGuid(), Name = input.Name.Trim(), Slug = slug, CreatedAtUtc = DateTime.UtcNow };
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
api.MapGet("/endpoints/{id:guid}/requests", async Task<Results<Ok<PagedResponse<RequestSummary>>, NotFound, ValidationProblem>> (Guid id, int? page, int? pageSize, StudioDbContext db) =>
{
    var p = page ?? 1; var size = pageSize ?? 25;
    if (p < 1 || size < 1 || size > maxPageSize)
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["pagination"] = ["page must be at least 1 and pageSize must be between 1 and 100."] });
    if (!await db.Endpoints.AnyAsync(x => x.Id == id)) return TypedResults.NotFound();
    var query = db.CapturedRequests.AsNoTracking().Where(x => x.EndpointId == id);
    var total = await query.CountAsync();
    var items = await query.OrderByDescending(x => x.ReceivedAtUtc).ThenByDescending(x => x.Id).Skip((p - 1) * size).Take(size).Select(x => x.ToSummary()).ToListAsync();
    return TypedResults.Ok(new PagedResponse<RequestSummary>(items, p, size, total));
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
api.MapPost("/requests/{id:guid}/replay", async Task<Results<Ok<ReplayResponse>, ValidationProblem, NotFound>> (Guid id, ReplayRequest input, StudioDbContext db, ReplayService replay, CancellationToken token) =>
{
    if (!Uri.TryCreate(input.TargetUrl, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https"))
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["targetUrl"] = ["Enter an absolute http or https URL."] });
    var captured = await db.CapturedRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token);
    if (captured is null) return TypedResults.NotFound();
    var result = await replay.ReplayAsync(captured, target, token);
    var attempt = new ReplayAttempt { Id = Guid.NewGuid(), CapturedRequestId = id, TargetUrl = target.ToString(), StatusCode = result.StatusCode, DurationMs = result.DurationMs, Succeeded = result.Succeeded, Error = result.Error, CreatedAtUtc = DateTime.UtcNow };
    db.ReplayAttempts.Add(attempt); await db.SaveChangesAsync(token);
    return TypedResults.Ok(new ReplayResponse(attempt.Id, attempt.StatusCode, attempt.DurationMs, attempt.Succeeded, attempt.Error, attempt.CreatedAtUtc));
}).WithOpenApi();

app.MapMethods("/hooks/{slug}/{**remainingPath}", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"], async (string slug, string? remainingPath, HttpContext context, StudioDbContext db, IHubContext<RequestHub> hub) =>
{
    var endpoint = await db.Endpoints.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower());
    if (endpoint is null) return Results.NotFound(new ProblemDetails { Title = "Endpoint not found" });
    if (context.Request.ContentLength > maxBodyBytes) return Results.Problem(statusCode: 413, title: "Request body too large", detail: "The maximum request body size is 1 MiB.");
    await using var body = new MemoryStream(); var buffer = new byte[81920]; int read;
    while ((read = await context.Request.Body.ReadAsync(buffer)) > 0)
    {
        if (body.Length + read > maxBodyBytes) return Results.Problem(statusCode: 413, title: "Request body too large", detail: "The maximum request body size is 1 MiB.");
        await body.WriteAsync(buffer.AsMemory(0, read));
    }
    var headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    var captured = new CapturedRequest { Id = Guid.NewGuid(), EndpointId = endpoint.Id, Method = context.Request.Method, PathAndQuery = "/" + (remainingPath ?? "") + context.Request.QueryString, HeadersJson = JsonSerializer.Serialize(headers), Body = body.ToArray(), BodySize = body.Length, ContentType = context.Request.ContentType, RemoteIp = context.Connection.RemoteIpAddress?.ToString(), ReceivedAtUtc = DateTime.UtcNow };
    db.CapturedRequests.Add(captured); await db.SaveChangesAsync();
    await hub.Clients.Group(endpoint.Id.ToString()).SendAsync("RequestCaptured", captured.ToSummary());
    return Results.Ok(new { requestId = captured.Id });
});
app.MapHub<RequestHub>("/hubs/requests");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapFallbackToFile("index.html");
app.Run();

static Dictionary<string, string[]> ValidateEndpoint(CreateEndpointRequest input)
{
    var errors = new Dictionary<string, string[]>();
    var name = input.Name?.Trim() ?? ""; var slug = input.Slug?.Trim() ?? "";
    if (name.Length is < 1 or > 80) errors["name"] = ["Name must be between 1 and 80 characters."];
    if (!Regex.IsMatch(slug, "^[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,78}[a-zA-Z0-9])?$")) errors["slug"] = ["Slug must be 1-80 letters, numbers, or hyphens and cannot begin or end with a hyphen."];
    return errors;
}
public partial class Program;
