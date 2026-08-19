using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace WebhookStudio.Api;

public interface IDnsResolver { Task<IPAddress[]> ResolveAsync(string host, CancellationToken token); }
public sealed class SystemDnsResolver : IDnsResolver { public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken token) => await Dns.GetHostAddressesAsync(host, token); }

public sealed class ReplaySecurity(IDnsResolver dns, IOptions<StudioOptions> options)
{
    public async Task<(bool Allowed, string? Code)> ValidateAsync(Uri uri, CancellationToken token)
    {
        if (uri.Scheme is not ("http" or "https")) return (false, "replay_scheme_blocked");
        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.Port is < 1 or > 65535) return (false, "replay_url_invalid");
        var host = uri.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)) return options.Value.AllowPrivateNetworkReplay ? (true, null) : (false, "replay_private_blocked");
        IPAddress[] addresses;
        try { addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await dns.ResolveAsync(host, token); }
        catch (SocketException) { return (false, "replay_dns_failed"); }
        if (addresses.Length == 0) return (false, "replay_dns_failed");
        return !options.Value.AllowPrivateNetworkReplay && addresses.Any(IsRestricted) ? (false, "replay_private_blocked") : (true, null);
    }

    public static bool IsRestricted(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None)) return true;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork) return bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 || bytes[0] is >= 224 || bytes[0] == 169 && bytes[1] == 254 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
        return address.IsIPv6LinkLocal || address.IsIPv6Multicast || bytes[0] is 0xfc or 0xfd;
    }
}

public static class ReplayHttpHandler
{
    public static HttpMessageHandler Create(IServiceProvider services)
    {
        var dns = services.GetRequiredService<IDnsResolver>(); var options = services.GetRequiredService<IOptions<StudioOptions>>();
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            UseProxy = false,
            ConnectCallback = async (context, token) =>
            {
                var addresses = IPAddress.TryParse(context.DnsEndPoint.Host, out var literal) ? [literal] : await dns.ResolveAsync(context.DnsEndPoint.Host, token);
                var allowed = options.Value.AllowPrivateNetworkReplay ? addresses : addresses.Where(x => !ReplaySecurity.IsRestricted(x)).ToArray();
                if (allowed.Length != addresses.Length || allowed.Length == 0) throw new HttpRequestException("Replay connection blocked by network safety policy.");
                Exception? last = null;
                foreach (var address in allowed) try { var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp); await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), token); return new NetworkStream(socket, ownsSocket: true); } catch (Exception ex) when (ex is SocketException or OperationCanceledException) { last = ex; }
                throw new HttpRequestException("Replay connection failed.", last);
            }
        };
    }
}
