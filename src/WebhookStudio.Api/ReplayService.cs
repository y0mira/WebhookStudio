using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WebhookStudio.Api;

public sealed class ReplayService(IHttpClientFactory clients, ReplaySecurity security, IOptions<StudioOptions> options)
{
    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase) { "Host", "Content-Length", "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Authorization", "Cookie" };

    public async Task<(int? StatusCode, long DurationMs, bool Succeeded, string? Error, string? Code)> ReplayAsync(CapturedRequest captured, Uri target, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.ReplayTimeoutSeconds));
        try
        {
            for (var redirect = 0; redirect <= options.Value.MaxReplayRedirects; redirect++)
            {
                var validation = await security.ValidateAsync(target, timeout.Token);
                if (!validation.Allowed) return (null, timer.ElapsedMilliseconds, false, "Replay target is blocked by the network safety policy.", validation.Code);
                using var message = CreateMessage(captured, target); using var response = await clients.CreateClient("replay").SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    if (redirect == options.Value.MaxReplayRedirects) return (null, timer.ElapsedMilliseconds, false, "Replay exceeded the redirect limit.", "replay_redirect_limit");
                    target = location.IsAbsoluteUri ? location : new Uri(target, location); continue;
                }
                if (response.Content.Headers.ContentLength > options.Value.MaxReplayResponseBytes) return (null, timer.ElapsedMilliseconds, false, "Replay response exceeded the size limit.", "replay_response_too_large");
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); var buffer = new byte[8192]; var total = 0; int read;
                while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0) if ((total += read) > options.Value.MaxReplayResponseBytes) return (null, timer.ElapsedMilliseconds, false, "Replay response exceeded the size limit.", "replay_response_too_large");
                return ((int)response.StatusCode, timer.ElapsedMilliseconds, true, null, null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return (null, timer.ElapsedMilliseconds, false, ex is TaskCanceledException ? "Replay timed out." : "Replay connection failed.", ex is TaskCanceledException ? "replay_timeout" : "replay_connection_failed"); }
        return (null, timer.ElapsedMilliseconds, false, "Replay failed.", "replay_failed");
    }

    private static HttpRequestMessage CreateMessage(CapturedRequest captured, Uri target)
    {
        var message = new HttpRequestMessage(new HttpMethod(captured.Method), target); if (captured.Body.Length > 0) message.Content = new ByteArrayContent(captured.Body);
        var headers = JsonSerializer.Deserialize<Dictionary<string, string[]>>(captured.HeadersJson) ?? [];
        foreach (var (name, values) in headers.Where(x => !BlockedHeaders.Contains(x.Key))) { if (!name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) continue; message.Content ??= new ByteArrayContent([]); message.Content.Headers.TryAddWithoutValidation(name, values); }
        return message;
    }
}
