using NickButlerAdroit.Api.Models;
using NickButlerAdroit.Api.Repositories;

namespace NickButlerAdroit.Tests.Unit;

public class InMemoryUrlRepositoryTests
{
    private readonly InMemoryUrlRepository _repo = new();

    [Fact]
    public async Task AddAsync_SucceedsForNewCode()
    {
        var entry = new ShortUrlEntry("abc", "https://example.com");

        Assert.True(await _repo.AddAsync(entry));
    }

    [Fact]
    public async Task AddAsync_FailsForDuplicateCode()
    {
        var entry = new ShortUrlEntry("abc", "https://example.com");
        await _repo.AddAsync(entry);

        var duplicate = new ShortUrlEntry("abc", "https://other.com");

        Assert.False(await _repo.AddAsync(duplicate));
    }

    [Fact]
    public async Task GetByShortCodeAsync_RetrievesExistingEntry()
    {
        var entry = new ShortUrlEntry("abc", "https://example.com");
        await _repo.AddAsync(entry);

        var result = await _repo.GetByShortCodeAsync("abc");

        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
    }

    [Fact]
    public async Task GetByShortCodeAsync_ReturnsNullForMissingCode()
    {
        var result = await _repo.GetByShortCodeAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingEntry()
    {
        var entry = new ShortUrlEntry("abc", "https://example.com");
        await _repo.AddAsync(entry);

        Assert.True(await _repo.DeleteAsync("abc"));

        var result = await _repo.GetByShortCodeAsync("abc");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForMissingCode()
    {
        Assert.False(await _repo.DeleteAsync("missing"));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://b.com"));

        var all = (await _repo.GetAllAsync()).ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetByLongUrlAsync_ReturnsMatchingEntries()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://example.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://example.com"));
        await _repo.AddAsync(new ShortUrlEntry("c", "https://other.com"));

        var results = (await _repo.GetByLongUrlAsync("https://example.com")).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("https://example.com", e.LongUrl));
    }

    [Fact]
    public async Task GetByLongUrlAsync_ReturnsEmptyForNoMatch()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://example.com"));

        var results = (await _repo.GetByLongUrlAsync("https://nomatch.com")).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsZeroWhenEmpty()
    {
        Assert.Equal(0, await _repo.GetCountAsync());
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://b.com"));

        Assert.Equal(2, await _repo.GetCountAsync());
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsRequestedPage()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://b.com"));
        await _repo.AddAsync(new ShortUrlEntry("c", "https://c.com"));

        var page = (await _repo.GetPagedAsync(0, 2)).ToList();

        Assert.Equal(2, page.Count);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsOrderedByCreatedAtDescending()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://b.com"));

        var page = (await _repo.GetPagedAsync(0, 10)).ToList();

        Assert.True(page[0].CreatedAt >= page[1].CreatedAt);
    }

    [Fact]
    public async Task GetPagedAsync_SkipsOffset()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://b.com"));
        await _repo.AddAsync(new ShortUrlEntry("c", "https://c.com"));

        var page = (await _repo.GetPagedAsync(2, 10)).ToList();

        Assert.Single(page);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsEmptyWhenOffsetExceedsCount()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));

        var page = (await _repo.GetPagedAsync(5, 10)).ToList();

        Assert.Empty(page);
    }

    [Fact]
    public async Task GetCountAsync_WithSearch_FiltersResults()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://example.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://other.com"));
        await _repo.AddAsync(new ShortUrlEntry("c", "https://example.org"));

        Assert.Equal(2, await _repo.GetCountAsync("example"));
    }

    [Fact]
    public async Task GetPagedAsync_WithSearch_FiltersResults()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://example.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://other.com"));
        await _repo.AddAsync(new ShortUrlEntry("c", "https://example.org"));

        var page = (await _repo.GetPagedAsync(0, 10, "example")).ToList();

        Assert.Equal(2, page.Count);
        Assert.All(page, e => Assert.Contains("example", e.LongUrl));
    }

    [Fact]
    public async Task GetPagedAsync_WithSearch_IsCaseInsensitive()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://Example.COM"));

        var page = (await _repo.GetPagedAsync(0, 10, "example")).ToList();

        Assert.Single(page);
    }

    [Fact]
    public async Task GetCountAsync_WithNullSearch_ReturnsAll()
    {
        await _repo.AddAsync(new ShortUrlEntry("a", "https://a.com"));
        await _repo.AddAsync(new ShortUrlEntry("b", "https://b.com"));

        Assert.Equal(2, await _repo.GetCountAsync(null));
    }

    // --- Case-insensitive short code tests ---

    [Fact]
    public async Task AddAsync_FailsForDuplicateCodeDifferentCase()
    {
        await _repo.AddAsync(new ShortUrlEntry("abcde", "https://example.com"));

        var duplicate = new ShortUrlEntry("ABCDE", "https://other.com");

        Assert.False(await _repo.AddAsync(duplicate));
    }

    [Fact]
    public async Task GetByShortCodeAsync_FindsEntryRegardlessOfCase()
    {
        await _repo.AddAsync(new ShortUrlEntry("abcde", "https://example.com"));

        Assert.NotNull(await _repo.GetByShortCodeAsync("ABCDE"));
        Assert.NotNull(await _repo.GetByShortCodeAsync("AbCdE"));
        Assert.NotNull(await _repo.GetByShortCodeAsync("abcde"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntryRegardlessOfCase()
    {
        await _repo.AddAsync(new ShortUrlEntry("abcde", "https://example.com"));

        Assert.True(await _repo.DeleteAsync("ABCDE"));
        Assert.Null(await _repo.GetByShortCodeAsync("abcde"));
    }

    [Fact]
    public async Task ExistsAsync_FindsEntryRegardlessOfCase()
    {
        await _repo.AddAsync(new ShortUrlEntry("abcde", "https://example.com"));

        Assert.True(await _repo.ExistsAsync("ABCDE"));
        Assert.True(await _repo.ExistsAsync("AbCdE"));
    }

    [Fact]
    public async Task IncrementClickCountAsync_WorksRegardlessOfCase()
    {
        await _repo.AddAsync(new ShortUrlEntry("abcde", "https://example.com"));

        var count = await _repo.IncrementClickCountAsync("ABCDE");

        Assert.Equal(1, count);
    }
}
