using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WebhookStudio.Api;

public sealed class ReplayService(IHttpClientFactory clients)
{
    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
        "TE", "Trailer", "Transfer-Encoding", "Upgrade"
    };

    public async Task<(int? StatusCode, long DurationMs, bool Succeeded, string? Error)> ReplayAsync(
        CapturedRequest captured, Uri target, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(new HttpMethod(captured.Method), target);
        if (captured.Body.Length > 0)
            message.Content = new ByteArrayContent(captured.Body);

        var headers = JsonSerializer.Deserialize<Dictionary<string, string[]>>(captured.HeadersJson) ?? [];
        foreach (var (name, values) in headers.Where(x => !BlockedHeaders.Contains(x.Key)))
        {
            if (name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                message.Content ??= new ByteArrayContent([]);
                message.Content.Headers.TryAddWithoutValidation(name, values);
            }
        }

        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await clients.CreateClient("replay").SendAsync(message, cancellationToken);
            timer.Stop();
            return ((int)response.StatusCode, timer.ElapsedMilliseconds, true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            timer.Stop();
            return (null, timer.ElapsedMilliseconds, false, ex.Message[..Math.Min(ex.Message.Length, 1000)]);
        }
    }
}
