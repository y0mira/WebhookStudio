using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WebhookStudio.Api;

namespace WebhookStudio.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<StudioDbContext>>();
            services.AddDbContext<StudioDbContext>(o => o.UseSqlite(_connection));
        });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _connection.Dispose(); }
}

public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    public ApiTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Endpoint_create_conflict_and_validation()
    {
        var created = await _client.PostAsJsonAsync("/api/endpoints", new { name = "Payments", slug = "payments" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conflict = await _client.PostAsJsonAsync("/api/endpoints", new { name = "Other", slug = "PAYMENTS" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var invalid = await _client.PostAsJsonAsync("/api/endpoints", new { name = "", slug = "not valid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Captures_get_query_and_post_json()
    {
        await CreateEndpoint("capture");
        var get = await _client.GetAsync("/hooks/capture/orders?state=new&tag=a&tag=b");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var content = new StringContent("{\"amount\":42}", Encoding.UTF8, "application/json");
        var post = await _client.PostAsync("/hooks/capture/payments/received?id=7", content);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var page = await Requests("capture");
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("POST", page.Items[0].Method);
        Assert.Equal("/payments/received?id=7", page.Items[0].PathAndQuery);
        var detail = await _client.GetFromJsonAsync<RequestDetail>($"/api/requests/{page.Items[0].Id}");
        Assert.Equal("{\"amount\":42}", Encoding.UTF8.GetString(Convert.FromBase64String(detail!.BodyBase64)));
        Assert.Equal("application/json; charset=utf-8", detail.ContentType);
    }

    [Fact]
    public async Task Body_over_one_mebibyte_is_rejected_and_not_stored()
    {
        await CreateEndpoint("large");
        var response = await _client.PostAsync("/hooks/large/", new ByteArrayContent(new byte[1024 * 1024 + 1]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty((await Requests("large")).Items);
    }

    [Fact]
    public async Task Request_list_is_newest_first_and_paged()
    {
        var endpoint = await CreateEndpoint("paging");
        for (var i = 0; i < 3; i++) await _client.PostAsync($"/hooks/paging/{i}", new StringContent(i.ToString()));
        var page = await _client.GetFromJsonAsync<PagedResponse<RequestSummary>>($"/api/endpoints/{endpoint.Id}/requests?page=2&pageSize=2");
        Assert.Equal(3, page!.Total); Assert.Single(page.Items); Assert.Equal("/0", page.Items[0].PathAndQuery);
    }

    private async Task<EndpointResponse> CreateEndpoint(string slug)
    {
        var response = await _client.PostAsJsonAsync("/api/endpoints", new { name = slug, slug }); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EndpointResponse>())!;
    }
    private async Task<PagedResponse<RequestSummary>> Requests(string slug)
    {
        var endpoints = await _client.GetFromJsonAsync<List<EndpointResponse>>("/api/endpoints");
        var endpoint = endpoints!.Single(x => x.Slug == slug);
        return (await _client.GetFromJsonAsync<PagedResponse<RequestSummary>>($"/api/endpoints/{endpoint.Id}/requests"))!;
    }
}

public sealed class ReplayTests
{
    [Fact]
    public async Task Replay_preserves_method_body_content_type_and_removes_host()
    {
        string? method = null, body = null, contentType = null, host = null;
        var receiver = new TestServer(new WebHostBuilder().Configure(app => app.Run(async context =>
        {
            method = context.Request.Method; host = context.Request.Headers.Host; contentType = context.Request.ContentType;
            using var reader = new StreamReader(context.Request.Body); body = await reader.ReadToEndAsync(); context.Response.StatusCode = 202;
        })));
        var factory = new FixedClientFactory(receiver.CreateClient());
        var service = new ReplayService(factory);
        var captured = new CapturedRequest { Id = Guid.NewGuid(), EndpointId = Guid.NewGuid(), Method = "PATCH", PathAndQuery = "/x", Body = Encoding.UTF8.GetBytes("{\"ok\":true}"), BodySize = 11, HeadersJson = JsonSerializer.Serialize(new Dictionary<string,string[]> { ["Host"]=["evil.example"], ["Content-Type"]=["application/json"] }), ReceivedAtUtc = DateTime.UtcNow };
        var result = await service.ReplayAsync(captured, new Uri("http://localhost/receive"), CancellationToken.None);
        Assert.True(result.Succeeded); Assert.Equal(202, result.StatusCode); Assert.Equal("PATCH", method); Assert.Equal("{\"ok\":true}", body); Assert.Equal("application/json", contentType); Assert.NotEqual("evil.example", host);
    }
    private sealed class FixedClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
}
