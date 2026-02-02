using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NickButlerAdroit.Api.Models;
using NickButlerAdroit.Api.Services;

namespace NickButlerAdroit.Tests.Integration;

public class UrlsControllerTests : IClassFixture<NoRateLimitWebApplicationFactory>
{
    private readonly NoRateLimitWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UrlsControllerTests(NoRateLimitWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_ReturnsAllUrls()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://one.com", "getall1"));
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://two.com", "getall2"));

        var response = await client.GetAsync("/api/urls");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<ShortUrlResult[]>();
        Assert.NotNull(results);
        Assert.True(results.Length >= 2);
    }

    [Fact]
    public async Task Post_WithValidUrl_Returns201()
    {
        var request = new CreateUrlRequest("https://example.com");

        var response = await _client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ShortUrlResult>();
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
    }

    [Fact]
    public async Task Post_WithInvalidUrl_Returns400()
    {
        var request = new CreateUrlRequest("not-a-url");

        var response = await _client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithCustomCode_ReturnsCustomCode()
    {
        var request = new CreateUrlRequest("https://example.com", "custom1");

        var response = await _client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ShortUrlResult>();
        Assert.NotNull(result);
        Assert.Equal("custom1", result.ShortCode);
    }

    [Fact]
    public async Task Post_WithDuplicateCode_Returns409()
    {
        var request = new CreateUrlRequest("https://example.com", "dupcode");
        await _client.PostAsJsonAsync("/api/urls", request);

        var response = await _client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExistingCode_Returns200()
    {
        var createRequest = new CreateUrlRequest("https://example.com", "gettest");
        await _client.PostAsJsonAsync("/api/urls", createRequest);

        var response = await _client.GetAsync("/api/urls/gettest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ShortUrlResult>();
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
    }

    [Fact]
    public async Task Get_MissingCode_Returns404()
    {
        var response = await _client.GetAsync("/api/urls/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidShortCode_Returns400()
    {
        var response = await _client.GetAsync("/api/urls/invalid-code!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_InvalidShortCode_Returns400()
    {
        var response = await _client.DeleteAsync("/api/urls/invalid-code!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_InvalidShortCode_Returns400()
    {
        var response = await _client.GetAsync("/api/urls/invalid-code!/stats");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Redirect_InvalidShortCode_Returns400()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/invalid-code!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingCode_Returns200()
    {
        var request = new CreateUrlRequest("https://example.com", "deltest");
        await _client.PostAsJsonAsync("/api/urls", request);

        var response = await _client.DeleteAsync("/api/urls/deltest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingCode_Returns404()
    {
        var response = await _client.DeleteAsync("/api/urls/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_ExistingCode_Returns200WithClickCount()
    {
        var request = new CreateUrlRequest("https://example.com", "statstest");
        await _client.PostAsJsonAsync("/api/urls", request);
        await _client.GetAsync("/api/urls/statstest");
        await _client.GetAsync("/api/urls/statstest");

        var response = await _client.GetAsync("/api/urls/statstest/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<UrlStats>();
        Assert.NotNull(stats);
        Assert.Equal(2, stats.ClickCount);
    }

    [Fact]
    public async Task GetStats_MissingCode_Returns404()
    {
        var response = await _client.GetAsync("/api/urls/missing/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaged_ReturnsPagedResult()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://paged1.com", "paged01"));
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://paged2.com", "paged02"));

        var response = await client.GetAsync("/api/urls/paged?offset=0&limit=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.True(doc.RootElement.TryGetProperty("totalCount", out var total));
        Assert.True(items.GetArrayLength() >= 2);
        Assert.True(total.GetInt32() >= 2);
    }

    [Fact]
    public async Task GetRecent_ReturnsRecentUrls()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://recent1.com", "recent1"));

        var response = await client.GetAsync("/api/urls/recent?count=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<ShortUrlResult[]>();
        Assert.NotNull(results);
        Assert.True(results.Length >= 1);
    }

    [Fact]
    public async Task GetPaged_ClampsLimitTo100()
    {
        var response = await _client.GetAsync("/api/urls/paged?offset=0&limit=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPaged_WithSearch_FiltersResults()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://searchmatch.com", "srch01"));
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://nomatch.com", "srch02"));

        var response = await client.GetAsync("/api/urls/paged?offset=0&limit=50&search=searchmatch");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        foreach (var item in items.EnumerateArray())
        {
            Assert.Contains("searchmatch", item.GetProperty("longUrl").GetString()!);
        }
    }

    [Fact]
    public async Task Redirect_ExistingCode_Returns302()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://redirect-target.com", "redir1"));

        var response = await client.GetAsync("/redir1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://redirect-target.com", response.Headers.Location?.ToString()!);
    }

    [Fact]
    public async Task Redirect_MissingCode_Returns404()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/nosuchcode");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ExceedingRateLimit_Returns429()
    {
        // Use a standard factory (with rate limiting enabled) for this test
        using var rateLimitedFactory = new WebApplicationFactory<Program>();
        var client = rateLimitedFactory.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest($"https://ratelimit{i}.com"));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        var response = await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://ratelimit-overflow.com"));
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_ExceedingRateLimit_Returns429()
    {
        // Use a standard factory (with rate limiting enabled) for this test
        using var rateLimitedFactory = new WebApplicationFactory<Program>();
        var client = rateLimitedFactory.CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://resolve-ratelimit.com", "reslim"));

        for (var i = 0; i < 30; i++)
        {
            var resp = await client.GetAsync("/api/urls/reslim");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        var response = await client.GetAsync("/api/urls/reslim");
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    // --- Non-alphanumeric character validation tests ---

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab$de")]
    [InlineData("ab%25de")]
    [InlineData("ab&de")]
    [InlineData("ab+de")]
    [InlineData("ab.de")]
    [InlineData("ab~de")]
    [InlineData("ab_de")]
    public async Task Get_NonAlphanumericShortCode_Returns400(string code)
    {
        // Note: # is excluded because HttpClient treats it as a URL fragment
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        var response = await client.GetAsync($"/api/urls/{code}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab$de")]
    [InlineData("ab_de")]
    [InlineData("ab.de")]
    public async Task Delete_NonAlphanumericShortCode_Returns400(string code)
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        var response = await client.DeleteAsync($"/api/urls/{code}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab$de")]
    [InlineData("ab_de")]
    [InlineData("ab.de")]
    public async Task GetStats_NonAlphanumericShortCode_Returns400(string code)
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        var response = await client.GetAsync($"/api/urls/{code}/stats");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab$de")]
    [InlineData("ab_de")]
    [InlineData("ab.de")]
    [InlineData("ab~de")]
    public async Task Redirect_NonAlphanumericShortCode_Returns400(string code)
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/{code}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab#de")]
    [InlineData("ab$de")]
    [InlineData("ab-de")]
    [InlineData("ab_de")]
    [InlineData("ab.de")]
    public async Task Post_NonAlphanumericCustomCode_Returns400(string code)
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        var response = await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://example.com", code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Case-insensitive short code tests ---

    [Fact]
    public async Task Post_WithMixedCaseCustomCode_ReturnsLowercaseCode()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        var request = new CreateUrlRequest("https://example.com", "CiTest1");

        var response = await client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ShortUrlResult>();
        Assert.NotNull(result);
        Assert.Equal("citest1", result.ShortCode);
    }

    [Fact]
    public async Task Post_DuplicateCodeDifferentCase_Returns409()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://example.com", "cidup1"));

        var response = await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://other.com", "CIDUP1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithDifferentCaseThanCreated_Returns200()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://example.com", "ciget1"));

        var response = await client.GetAsync("/api/urls/CIGET1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ShortUrlResult>();
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
    }

    [Fact]
    public async Task Delete_WithDifferentCaseThanCreated_Returns200()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://example.com", "cidel1"));

        var response = await client.DeleteAsync("/api/urls/CIDEL1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithDifferentCaseThanCreated_Returns200()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://example.com", "cistat1"));
        await client.GetAsync("/api/urls/cistat1");

        var response = await client.GetAsync("/api/urls/CISTAT1/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<UrlStats>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats.ClickCount);
    }

    [Fact]
    public async Task Redirect_WithDifferentCaseThanCreated_Returns302()
    {
        var client = _factory.WithWebHostBuilder(_ => { }).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsJsonAsync("/api/urls", new CreateUrlRequest("https://redirect-ci.com", "cirdr1"));

        var response = await client.GetAsync("/CIRDR1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://redirect-ci.com", response.Headers.Location?.ToString()!);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithJsonError()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(IUrlShortenerService));
                services.Remove(descriptor);
                services.AddSingleton<IUrlShortenerService, ThrowingUrlShortenerService>();
            });
        }).CreateClient();

        var request = new CreateUrlRequest("https://example.com");
        var response = await client.PostAsJsonAsync("/api/urls", request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.False(string.IsNullOrEmpty(errorProp.GetString()));
    }
}

internal class ThrowingUrlShortenerService : IUrlShortenerService
{
    public Task<IReadOnlyList<ShortUrlResult>> GetAllAsync()
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task<PagedResult<ShortUrlResult>> GetPagedAsync(int offset, int limit, string? search = null)
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task<IReadOnlyList<ShortUrlResult>> GetRecentAsync(int count)
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task<ShortUrlResult> CreateAsync(string longUrl, string? customCode = null)
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task<ShortUrlResult> ResolveAsync(string shortCode)
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task<string> ResolveForRedirectAsync(string shortCode)
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task<UrlStats> GetStatsAsync(string shortCode)
        => throw new InvalidOperationException("Simulated unexpected error");

    public Task DeleteAsync(string shortCode)
        => throw new InvalidOperationException("Simulated unexpected error");
}
