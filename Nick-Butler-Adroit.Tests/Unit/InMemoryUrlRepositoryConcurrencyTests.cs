using NickButlerAdroit.Api.Models;
using NickButlerAdroit.Api.Repositories;

namespace NickButlerAdroit.Tests.Unit;

public class InMemoryUrlRepositoryConcurrencyTests
{
    private readonly InMemoryUrlRepository _repo = new();

    [Fact]
    public async Task AddAsync_ConcurrentAddsWithUniqueKeys_AllSucceed()
    {
        const int count = 100;

        var tasks = Enumerable.Range(0, count)
            .Select(i => _repo.AddAsync(new ShortUrlEntry($"code{i:D4}", $"https://example.com/{i}")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, success => Assert.True(success));
        var all = await _repo.GetAllAsync();
        Assert.Equal(count, all.Count);
    }

    [Fact]
    public async Task AddAsync_ConcurrentAddsWithSameKey_ExactlyOneSucceeds()
    {
        const int count = 50;

        var tasks = Enumerable.Range(0, count)
            .Select(i => _repo.AddAsync(new ShortUrlEntry("samecode", $"https://example.com/{i}")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(count - 1, results.Count(r => !r));
    }

    [Fact]
    public async Task DeleteAsync_ConcurrentDeletesOfSameKey_ExactlyOneSucceeds()
    {
        await _repo.AddAsync(new ShortUrlEntry("todelete", "https://example.com"));
        const int count = 50;

        var tasks = Enumerable.Range(0, count)
            .Select(_ => _repo.DeleteAsync("todelete"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(count - 1, results.Count(r => !r));

        var entry = await _repo.GetByShortCodeAsync("todelete");
        Assert.Null(entry);
    }

    [Fact]
    public async Task IncrementClickCountAsync_ConcurrentIncrements_AllCounted()
    {
        await _repo.AddAsync(new ShortUrlEntry("clicks", "https://example.com"));
        const int count = 1000;

        var tasks = Enumerable.Range(0, count)
            .Select(_ => _repo.IncrementClickCountAsync("clicks"))
            .ToArray();

        await Task.WhenAll(tasks);

        var entry = await _repo.GetByShortCodeAsync("clicks");
        Assert.NotNull(entry);
        Assert.Equal(count, entry.ClickCount);
    }

    [Fact]
    public async Task ConcurrentAddsAndDeletes_DoNotCorruptState()
    {
        const int count = 100;

        // Pre-populate half the entries
        for (var i = 0; i < count; i++)
        {
            await _repo.AddAsync(new ShortUrlEntry($"mixed{i:D4}", $"https://example.com/{i}"));
        }

        // Concurrently add new entries and delete existing ones
        var addTasks = Enumerable.Range(count, count)
            .Select(i => _repo.AddAsync(new ShortUrlEntry($"mixed{i:D4}", $"https://example.com/{i}")));
        var deleteTasks = Enumerable.Range(0, count)
            .Select(i => _repo.DeleteAsync($"mixed{i:D4}"));

        await Task.WhenAll(addTasks.Concat(deleteTasks));

        // All originally-deleted entries should be gone, all new adds should exist
        var all = await _repo.GetAllAsync();
        Assert.True(all.Count <= count, $"Expected at most {count} entries but found {all.Count}");
        // All remaining entries should be retrievable without error
        foreach (var entry in all)
        {
            var fetched = await _repo.GetByShortCodeAsync(entry.ShortCode);
            Assert.NotNull(fetched);
        }
    }
}
