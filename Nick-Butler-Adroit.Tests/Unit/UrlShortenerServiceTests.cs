using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NickButlerAdroit.Api.Exceptions;
using NickButlerAdroit.Api.Hubs;
using NickButlerAdroit.Api.Repositories;
using NickButlerAdroit.Api.Services;

namespace NickButlerAdroit.Tests.Unit;

public class UrlShortenerServiceTests
{
    private readonly UrlShortenerService _service;
    private readonly InMemoryUrlRepository _repo;

    public UrlShortenerServiceTests()
    {
        _repo = new InMemoryUrlRepository();
        var hubContext = Substitute.For<IHubContext<UrlHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        clients.All.Returns(clientProxy);
        hubContext.Clients.Returns(clients);
        _service = new UrlShortenerService(_repo, hubContext, NullLogger<UrlShortenerService>.Instance);
    }

    [Fact]
    public async Task Create_WithValidUrl_ReturnsResult()
    {
        var result = await _service.CreateAsync("https://example.com");

        Assert.Equal("https://example.com", result.LongUrl);
        Assert.NotEmpty(result.ShortCode);
        Assert.Equal(7, result.ShortCode.Length);
    }

    [Fact]
    public async Task Create_WithInvalidUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("not-a-url"));
    }

    [Fact]
    public async Task Create_WithNonHttpScheme_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("ftp://example.com"));
    }

    [Fact]
    public async Task Create_NormalizesHostToLowercase()
    {
        var result = await _service.CreateAsync("https://EXAMPLE.COM/Path", "norm01");

        Assert.Equal("https://example.com/Path", result.LongUrl);
    }

    [Fact]
    public async Task Create_RemovesTrailingSlash()
    {
        var result = await _service.CreateAsync("https://example.com/path/", "norm02");

        Assert.Equal("https://example.com/path", result.LongUrl);
    }

    [Fact]
    public async Task Create_RemovesRootSlash()
    {
        var result = await _service.CreateAsync("https://example.com/", "norm03");

        Assert.Equal("https://example.com", result.LongUrl);
    }

    [Fact]
    public async Task Create_RemovesDefaultHttpsPort()
    {
        var result = await _service.CreateAsync("https://example.com:443/path", "norm04");

        Assert.Equal("https://example.com/path", result.LongUrl);
    }

    [Fact]
    public async Task Create_RemovesDefaultHttpPort()
    {
        var result = await _service.CreateAsync("http://example.com:80/path", "norm05");

        Assert.Equal("http://example.com/path", result.LongUrl);
    }

    [Fact]
    public async Task Create_KeepsNonDefaultPort()
    {
        var result = await _service.CreateAsync("https://example.com:8080/path", "norm06");

        Assert.Equal("https://example.com:8080/path", result.LongUrl);
    }

    [Fact]
    public async Task Create_PreservesQueryString()
    {
        var result = await _service.CreateAsync("https://example.com/path?foo=bar", "norm07");

        Assert.Equal("https://example.com/path?foo=bar", result.LongUrl);
    }

    [Fact]
    public async Task Create_NormalizedUrlsShareLongUrlClickCount()
    {
        await _service.CreateAsync("https://EXAMPLE.COM/path/", "shared1");
        await _service.CreateAsync("https://example.com/path", "shared2");

        await _service.ResolveAsync("shared1");
        var result = await _service.ResolveAsync("shared2");

        Assert.Equal(2, result.LongUrlClickCount);
    }

    [Fact]
    public async Task Create_WithCustomCode_UsesCustomCode()
    {
        var result = await _service.CreateAsync("https://example.com", "mycode");

        Assert.Equal("mycode", result.ShortCode);
    }

    [Fact]
    public async Task Create_WithDuplicateCustomCode_ThrowsDuplicateShortCodeException()
    {
        await _service.CreateAsync("https://example.com", "mycode");

        await Assert.ThrowsAsync<DuplicateShortCodeException>(
            () => _service.CreateAsync("https://other.com", "mycode"));
    }

    [Fact]
    public async Task Create_WithTooShortCustomCode_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("https://example.com", "abcd"));
    }

    [Fact]
    public async Task Create_WithExactMinLengthCustomCode_Succeeds()
    {
        var result = await _service.CreateAsync("https://example.com", "abcde");

        Assert.Equal("abcde", result.ShortCode);
    }

    [Fact]
    public async Task Create_WithInvalidCustomCodeChars_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("https://example.com", "my code!"));
    }

    [Fact]
    public async Task Create_WithHyphenInCustomCode_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("https://example.com", "my-code"));
    }

    [Fact]
    public async Task Create_WithUnderscoreInCustomCode_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("https://example.com", "my_code"));
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab#de")]
    [InlineData("ab$de")]
    [InlineData("ab%de")]
    [InlineData("ab&de")]
    [InlineData("ab+de")]
    [InlineData("ab=de")]
    [InlineData("ab/de")]
    [InlineData("ab\\de")]
    [InlineData("ab.de")]
    [InlineData("ab de")]
    [InlineData("ab\tde")]
    [InlineData("ab<de")]
    [InlineData("ab>de")]
    [InlineData("ab{de")]
    [InlineData("ab}de")]
    [InlineData("ab|de")]
    [InlineData("ab^de")]
    [InlineData("ab~de")]
    [InlineData("ab`de")]
    [InlineData("ab'de")]
    [InlineData("ab\"de")]
    public async Task Create_WithNonAlphanumericChars_ThrowsArgumentException(string code)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("https://example.com", code));
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab#de")]
    [InlineData("ab$de")]
    [InlineData("ab%de")]
    [InlineData("ab.de")]
    [InlineData("ab/de")]
    [InlineData("ab de")]
    public async Task Resolve_WithNonAlphanumericChars_ThrowsArgumentException(string code)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ResolveAsync(code));
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab#de")]
    [InlineData("ab$de")]
    public async Task ResolveForRedirect_WithNonAlphanumericChars_ThrowsArgumentException(string code)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ResolveForRedirectAsync(code));
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab#de")]
    [InlineData("ab$de")]
    public async Task GetStats_WithNonAlphanumericChars_ThrowsArgumentException(string code)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetStatsAsync(code));
    }

    [Theory]
    [InlineData("ab@de")]
    [InlineData("ab#de")]
    [InlineData("ab$de")]
    public async Task Delete_WithNonAlphanumericChars_ThrowsArgumentException(string code)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteAsync(code));
    }

    [Fact]
    public async Task Resolve_IncrementsClickCount()
    {
        await _service.CreateAsync("https://example.com", "test123");

        var resolved = await _service.ResolveAsync("test123");

        Assert.Equal(1, resolved.ClickCount);
    }

    [Fact]
    public async Task Resolve_MissingCode_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ResolveAsync("missing"));
    }

    [Fact]
    public async Task ResolveForRedirect_ReturnsLongUrl()
    {
        await _service.CreateAsync("https://example.com", "redir1");

        var longUrl = await _service.ResolveForRedirectAsync("redir1");

        Assert.Equal("https://example.com", longUrl);
    }

    [Fact]
    public async Task ResolveForRedirect_IncrementsClickCount()
    {
        await _service.CreateAsync("https://example.com", "redir2");

        await _service.ResolveForRedirectAsync("redir2");
        var stats = await _service.GetStatsAsync("redir2");

        Assert.Equal(1, stats.ClickCount);
    }

    [Fact]
    public async Task ResolveForRedirect_MissingCode_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ResolveForRedirectAsync("missing"));
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _service.CreateAsync("https://example.com", "todelete");

        await _service.DeleteAsync("todelete");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ResolveAsync("todelete"));
    }

    [Fact]
    public async Task Delete_MissingCode_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteAsync("missing"));
    }

    [Fact]
    public async Task GetStats_ReturnsClickCount()
    {
        await _service.CreateAsync("https://example.com", "stats1");
        await _service.ResolveAsync("stats1");
        await _service.ResolveAsync("stats1");

        var stats = await _service.GetStatsAsync("stats1");

        Assert.Equal(2, stats.ClickCount);
        Assert.Equal("stats1", stats.ShortCode);
    }

    [Fact]
    public async Task GetStats_MissingCode_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetStatsAsync("missing"));
    }

    [Fact]
    public async Task Create_ReturnsZeroLongUrlClickCount()
    {
        var result = await _service.CreateAsync("https://example.com", "agg001");

        Assert.Equal(0, result.LongUrlClickCount);
    }

    [Fact]
    public async Task Resolve_AggregatesClicksAcrossSharedLongUrl()
    {
        await _service.CreateAsync("https://example.com", "agg100");
        await _service.CreateAsync("https://example.com", "agg200");

        await _service.ResolveAsync("agg100");
        await _service.ResolveAsync("agg100");
        await _service.ResolveAsync("agg200");

        var result = await _service.ResolveAsync("agg100");

        Assert.Equal(3, result.ClickCount);
        Assert.Equal(4, result.LongUrlClickCount);
    }

    [Fact]
    public async Task GetPaged_ReturnsPagedResult()
    {
        await _service.CreateAsync("https://a.com", "paged1");
        await _service.CreateAsync("https://b.com", "paged2");
        await _service.CreateAsync("https://c.com", "paged3");

        var result = await _service.GetPagedAsync(0, 2);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetPaged_OffsetSkipsItems()
    {
        await _service.CreateAsync("https://a.com", "pgoff1");
        await _service.CreateAsync("https://b.com", "pgoff2");
        await _service.CreateAsync("https://c.com", "pgoff3");

        var result = await _service.GetPagedAsync(2, 10);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetRecent_ReturnsRecentItems()
    {
        await _service.CreateAsync("https://a.com", "rec001");
        await _service.CreateAsync("https://b.com", "rec002");
        await _service.CreateAsync("https://c.com", "rec003");

        var results = await _service.GetRecentAsync(2);

        Assert.Equal(2, results.Count);
    }

    // --- Case-insensitive short code tests ---

    [Fact]
    public async Task Create_WithMixedCaseCustomCode_StoresAsLowercase()
    {
        var result = await _service.CreateAsync("https://example.com", "AbCdE");

        Assert.Equal("abcde", result.ShortCode);
    }

    [Fact]
    public async Task Create_WithUppercaseCustomCode_StoresAsLowercase()
    {
        var result = await _service.CreateAsync("https://example.com", "UPPER");

        Assert.Equal("upper", result.ShortCode);
    }

    [Fact]
    public async Task Create_AutoGeneratedCode_IsAllLowercase()
    {
        var result = await _service.CreateAsync("https://example.com");

        Assert.Equal(result.ShortCode, result.ShortCode.ToLowerInvariant());
    }

    [Fact]
    public async Task Create_DuplicateCustomCodeDifferentCase_ThrowsDuplicateShortCodeException()
    {
        await _service.CreateAsync("https://example.com", "citest");

        await Assert.ThrowsAsync<DuplicateShortCodeException>(
            () => _service.CreateAsync("https://other.com", "CITEST"));
    }

    [Fact]
    public async Task Resolve_WithDifferentCase_ReturnsResult()
    {
        await _service.CreateAsync("https://example.com", "cireso");

        var result = await _service.ResolveAsync("CIRESO");

        Assert.Equal("https://example.com", result.LongUrl);
    }

    [Fact]
    public async Task Resolve_WithMixedCase_IncrementsClickCount()
    {
        await _service.CreateAsync("https://example.com", "ciclck");

        await _service.ResolveAsync("CiClCk");
        var result = await _service.ResolveAsync("CICLCK");

        Assert.Equal(2, result.ClickCount);
    }

    [Fact]
    public async Task ResolveForRedirect_WithDifferentCase_ReturnsLongUrl()
    {
        await _service.CreateAsync("https://example.com", "cirdir");

        var longUrl = await _service.ResolveForRedirectAsync("CIRDIR");

        Assert.Equal("https://example.com", longUrl);
    }

    [Fact]
    public async Task GetStats_WithDifferentCase_ReturnsStats()
    {
        await _service.CreateAsync("https://example.com", "cistas");
        await _service.ResolveAsync("cistas");

        var stats = await _service.GetStatsAsync("CISTAS");

        Assert.Equal(1, stats.ClickCount);
        Assert.Equal("cistas", stats.ShortCode);
    }

    [Fact]
    public async Task Delete_WithDifferentCase_RemovesEntry()
    {
        await _service.CreateAsync("https://example.com", "cidelt");

        await _service.DeleteAsync("CIDELT");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ResolveAsync("cidelt"));
    }
}
