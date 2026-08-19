using System.ComponentModel.DataAnnotations;

namespace WebhookStudio.Api;

public sealed class StudioOptions
{
    public const string Section = "WebhookStudio";
    [Range(1024, 10 * 1024 * 1024)] public int MaxBodyBytes { get; set; } = 1024 * 1024;
    [Range(10, 10000)] public int DefaultRetentionLimit { get; set; } = 500;
    [Range(1, 200)] public int MaxHeaderCount { get; set; } = 100;
    [Range(128, 32768)] public int MaxHeaderValueLength { get; set; } = 8192;
    [Range(128, 16384)] public int MaxPathLength { get; set; } = 4096;
    public bool AllowPrivateNetworkReplay { get; set; }
    [Range(0, 10)] public int MaxReplayRedirects { get; set; } = 5;
    [Range(1, 120)] public int ReplayTimeoutSeconds { get; set; } = 15;
    [Range(1024, 10 * 1024 * 1024)] public int MaxReplayResponseBytes { get; set; } = 1024 * 1024;
}
