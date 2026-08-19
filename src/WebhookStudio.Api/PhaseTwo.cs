using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebhookStudio.Api;

public static class PhaseTwo
{
    public static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase) { "Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization" };
    public static Dictionary<string, string[]> SafeHeaders(string json, bool includeSensitive) =>
        (JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? [])
        .Where(x => includeSensitive || !SensitiveHeaders.Contains(x.Key))
        .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    public static string? DisplayText(byte[] body, string? contentType)
    {
        if (body.Length == 0) return "";
        try { return new UTF8Encoding(false, true).GetString(body); } catch { return null; }
    }

    public static IReadOnlyList<DiffItem> Compare(CapturedRequest left, CapturedRequest right)
    {
        var result = new List<DiffItem>();
        Add(result, "method", left.Method, right.Method); Add(result, "path", left.PathAndQuery, right.PathAndQuery);
        var lh = SafeHeaders(left.HeadersJson, false); var rh = SafeHeaders(right.HeadersJson, false);
        foreach (var key in lh.Keys.Union(rh.Keys, StringComparer.OrdinalIgnoreCase).Order())
            Add(result, $"headers.{key}", lh.GetValueOrDefault(key) is { } a ? string.Join(", ", a) : null, rh.GetValueOrDefault(key) is { } b ? string.Join(", ", b) : null);
        if (TryJson(left.BodyText, out var lj) && TryJson(right.BodyText, out var rj)) CompareJson(result, "body", lj, rj);
        else if (left.BodyText is not null && right.BodyText is not null) Add(result, "body", left.BodyText, right.BodyText);
        else Add(result, "body.sha256", Hash(left.Body), Hash(right.Body));
        return result;
    }

    private static void Add(List<DiffItem> r, string path, string? left, string? right)
    { if (left == right) return; r.Add(new(path, left is null ? "added" : right is null ? "removed" : "changed", left, right)); }
    private static bool TryJson(string? value, out JsonElement element) { try { element = JsonSerializer.Deserialize<JsonElement>(value!); return value is not null; } catch { element = default; return false; } }
    private static void CompareJson(List<DiffItem> r, string path, JsonElement l, JsonElement x)
    {
        if (l.ValueKind == JsonValueKind.Object && x.ValueKind == JsonValueKind.Object) { var lp = l.EnumerateObject().ToDictionary(p => p.Name, p => p.Value); var rp = x.EnumerateObject().ToDictionary(p => p.Name, p => p.Value); foreach (var k in lp.Keys.Union(rp.Keys).Order()) if (lp.TryGetValue(k, out var a) && rp.TryGetValue(k, out var b)) CompareJson(r, $"{path}.{k}", a, b); else Add(r, $"{path}.{k}", lp.TryGetValue(k, out a) ? a.ToString() : null, rp.TryGetValue(k, out b) ? b.ToString() : null); return; }
        Add(r, path, l.ToString(), x.ToString());
    }
    private static string Hash(byte[] body) => Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
}
