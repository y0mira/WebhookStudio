using System.Text.Json;

namespace WebhookStudio.Api;

public sealed record CreateEndpointRequest(string Name, string Slug);
public sealed record ReplayRequest(string TargetUrl);
public sealed record EndpointResponse(Guid Id, string Name, string Slug, DateTime CreatedAtUtc);
public sealed record RequestSummary(Guid Id, string Method, string PathAndQuery, string? ContentType, long BodySize, DateTime ReceivedAtUtc);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed record RequestDetail(Guid Id, Guid EndpointId, string Method, string PathAndQuery,
    Dictionary<string, string[]> Headers, string BodyBase64, string? ContentType, string? RemoteIp,
    DateTime ReceivedAtUtc, long BodySize);
public sealed record ReplayResponse(Guid Id, int? StatusCode, long DurationMs, bool Succeeded, string? Error, DateTime CreatedAtUtc);

public static class Mappings
{
    public static EndpointResponse ToResponse(this Endpoint x) => new(x.Id, x.Name, x.Slug, x.CreatedAtUtc);
    public static RequestSummary ToSummary(this CapturedRequest x) => new(x.Id, x.Method, x.PathAndQuery, x.ContentType, x.BodySize, x.ReceivedAtUtc);
    public static RequestDetail ToDetail(this CapturedRequest x) => new(x.Id, x.EndpointId, x.Method, x.PathAndQuery,
        JsonSerializer.Deserialize<Dictionary<string, string[]>>(x.HeadersJson) ?? [], Convert.ToBase64String(x.Body),
        x.ContentType, x.RemoteIp, x.ReceivedAtUtc, x.BodySize);
}
