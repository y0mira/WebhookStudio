using Microsoft.EntityFrameworkCore;

namespace WebhookStudio.Api;

public sealed class Endpoint
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<CapturedRequest> Requests { get; set; } = [];
}

public sealed class CapturedRequest
{
    public Guid Id { get; set; }
    public Guid EndpointId { get; set; }
    public Endpoint? Endpoint { get; set; }
    public required string Method { get; set; }
    public required string PathAndQuery { get; set; }
    public required string HeadersJson { get; set; }
    public required byte[] Body { get; set; }
    public string? ContentType { get; set; }
    public string? RemoteIp { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public long BodySize { get; set; }
    public List<ReplayAttempt> ReplayAttempts { get; set; } = [];
}

public sealed class ReplayAttempt
{
    public Guid Id { get; set; }
    public Guid CapturedRequestId { get; set; }
    public CapturedRequest? CapturedRequest { get; set; }
    public required string TargetUrl { get; set; }
    public int? StatusCode { get; set; }
    public long DurationMs { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<Endpoint> Endpoints => Set<Endpoint>();
    public DbSet<CapturedRequest> CapturedRequests => Set<CapturedRequest>();
    public DbSet<ReplayAttempt> ReplayAttempts => Set<ReplayAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Endpoint>(b =>
        {
            b.HasIndex(x => x.Slug).IsUnique();
            b.Property(x => x.Name).HasMaxLength(80);
            b.Property(x => x.Slug).HasMaxLength(80);
        });
        modelBuilder.Entity<CapturedRequest>(b =>
        {
            b.HasIndex(x => new { x.EndpointId, x.ReceivedAtUtc });
            b.HasOne(x => x.Endpoint).WithMany(x => x.Requests).HasForeignKey(x => x.EndpointId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ReplayAttempt>(b =>
        {
            b.Property(x => x.Error).HasMaxLength(1000);
            b.HasOne(x => x.CapturedRequest).WithMany(x => x.ReplayAttempts).HasForeignKey(x => x.CapturedRequestId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
