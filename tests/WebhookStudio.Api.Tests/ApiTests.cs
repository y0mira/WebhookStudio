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
    private readonly string _connectionString = $"Data Source=tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private SqliteConnection? _keepAlive;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive = new SqliteConnection(_connectionString); _keepAlive.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<StudioDbContext>>();
            services.AddDbContext<StudioDbContext>(o => o.UseSqlite(_connectionString));
        });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _keepAlive?.Dispose(); }
}

public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiFactory _factory;
    public ApiTests(ApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }

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

    [Fact]
    public async Task Filters_search_and_stable_pagination_are_combined()
    {
        var endpoint=await CreateEndpoint("filters");
        await _client.PostAsync("/hooks/filters/orders?kind=alpha",new StringContent("needle one",Encoding.UTF8,"text/plain"));
        await _client.PostAsync("/hooks/filters/orders?kind=beta",new StringContent("needle two",Encoding.UTF8,"text/plain"));
        await _client.PostAsync("/hooks/filters/other",new StringContent("ignored",Encoding.UTF8,"text/plain"));
        var page=await _client.GetFromJsonAsync<PagedResponse<RequestSummary>>($"/api/endpoints/{endpoint.Id}/requests?page=1&pageSize=1&method=POST&statusCategory=2&search=needle");
        Assert.Equal(2,page!.Total);Assert.Single(page.Items);Assert.Equal("/orders?kind=beta",page.Items[0].PathAndQuery);
    }

    [Fact]
    public async Task Custom_response_and_async_delay_are_applied()
    {
        var endpoint=await CreateEndpoint("response");
        var settings=await _client.PutAsJsonAsync($"/api/endpoints/{endpoint.Id}/settings",new{responseStatusCode=202,responseContentType="text/plain",responseBody="queued",responseDelayMs=80,retentionLimit=500});settings.EnsureSuccessStatusCode();
        var timer=System.Diagnostics.Stopwatch.StartNew();var result=await _client.PostAsync("/hooks/response/",new StringContent("x"));timer.Stop();
        Assert.Equal(HttpStatusCode.Accepted,result.StatusCode);Assert.Equal("text/plain",result.Content.Headers.ContentType!.MediaType);Assert.Equal("queued",await result.Content.ReadAsStringAsync());Assert.True(timer.ElapsedMilliseconds>=60);
    }

    [Fact]
    public async Task Retention_is_bounded_during_concurrent_capture()
    {
        var endpoint=await CreateEndpoint("retention");
        (await _client.PutAsJsonAsync($"/api/endpoints/{endpoint.Id}/settings",new{responseStatusCode=200,responseContentType="application/json",responseBody="{}",responseDelayMs=0,retentionLimit=10})).EnsureSuccessStatusCode();
        await Task.WhenAll(Enumerable.Range(0,16).Select(i=>_client.PostAsync($"/hooks/retention/{i}",new StringContent(i.ToString()))));
        var page=await _client.GetFromJsonAsync<PagedResponse<RequestSummary>>($"/api/endpoints/{endpoint.Id}/requests?page=1&pageSize=25");Assert.Equal(10,page!.Total);
    }

    [Fact]
    public async Task Export_redacts_sensitive_headers_and_round_trips_import()
    {
        var source=await CreateEndpoint("export-source");var message=new HttpRequestMessage(HttpMethod.Post,"/hooks/export-source/path"){Content=new StringContent("{\"a\":1}",Encoding.UTF8,"application/json")};message.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer","secret");message.Headers.Add("X-Keep","yes");await _client.SendAsync(message);
        var package=await _client.GetFromJsonAsync<ExportPackage>($"/api/endpoints/{source.Id}/export");Assert.DoesNotContain(package!.Requests[0].Headers.Keys,x=>x.Equals("Authorization",StringComparison.OrdinalIgnoreCase));Assert.Contains("X-Keep",package.Requests[0].Headers.Keys);
        var target=await CreateEndpoint("export-target");var imported=await _client.PostAsJsonAsync($"/api/endpoints/{target.Id}/import",package);imported.EnsureSuccessStatusCode();Assert.Equal(1,(await imported.Content.ReadFromJsonAsync<ImportResult>())!.Imported);
        var rows=await _client.GetFromJsonAsync<PagedResponse<RequestSummary>>($"/api/endpoints/{target.Id}/requests");Assert.Single(rows!.Items);
        var invalid=await _client.PostAsync($"/api/endpoints/{target.Id}/import",new StringContent("not json",Encoding.UTF8,"application/json"));Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        var huge=new ByteArrayContent(new byte[5*1024*1024+1]);huge.Headers.ContentType=new("application/json");Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await _client.PostAsync($"/api/endpoints/{target.Id}/import",huge)).StatusCode);
    }

    [Fact]
    public async Task Json_diff_is_structural_and_reports_semantics()
    {
        var endpoint=await CreateEndpoint("diff");await _client.PostAsync("/hooks/diff/one",new StringContent("{\"same\":1,\"removed\":true,\"change\":1}",Encoding.UTF8,"application/json"));await _client.PostAsync("/hooks/diff/two",new StringContent("{\"same\":1,\"added\":true,\"change\":2}",Encoding.UTF8,"application/json"));var rows=await _client.GetFromJsonAsync<PagedResponse<RequestSummary>>($"/api/endpoints/{endpoint.Id}/requests");var response=await _client.PostAsJsonAsync($"/api/endpoints/{endpoint.Id}/compare",new{leftId=rows!.Items[1].Id,rightId=rows.Items[0].Id});var diff=await response.Content.ReadFromJsonAsync<CompareResponse>();Assert.Contains(diff!.Differences,x=>x.Path=="body.added"&&x.Kind=="added");Assert.Contains(diff.Differences,x=>x.Path=="body.removed"&&x.Kind=="removed");Assert.Contains(diff.Differences,x=>x.Path=="body.change"&&x.Kind=="changed");
    }

    [Fact]
    public async Task Filter_query_plan_uses_endpoint_method_time_index()
    {
        await CreateEndpoint("query-plan");
        using var scope=_factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<StudioDbContext>();await db.Database.OpenConnectionAsync();
        await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText="EXPLAIN QUERY PLAN SELECT * FROM CapturedRequests WHERE EndpointId = $endpoint AND Method = $method ORDER BY ReceivedAtUtc DESC LIMIT 25";command.Parameters.Add(new SqliteParameter("$endpoint",Guid.NewGuid()));command.Parameters.Add(new SqliteParameter("$method","POST"));
        await using var reader=await command.ExecuteReaderAsync();var details=new List<string>();while(await reader.ReadAsync())details.Add(reader.GetString(3));
        Assert.Contains(details,x=>x.Contains("IX_CapturedRequests_EndpointId_Method_ReceivedAtUtc",StringComparison.Ordinal));
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
