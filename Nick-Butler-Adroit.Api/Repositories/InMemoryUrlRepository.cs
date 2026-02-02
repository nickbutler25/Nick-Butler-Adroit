using System.Collections.Concurrent;
using NickButlerAdroit.Api.Models;

namespace NickButlerAdroit.Api.Repositories;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IUrlRepository"/>.
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by short code for O(1) lookups.
/// All mutations (add, delete, increment) are atomic — no check-then-act race conditions.
/// Methods return Task to match the async interface, but execute synchronously via Task.FromResult
/// since there is no actual I/O. Registered as a singleton in DI so all requests share one store.
/// </summary>
public class InMemoryUrlRepository : IUrlRepository
{
    /// <summary>Primary storage: short code → URL entry. ConcurrentDictionary provides thread safety.</summary>
    private readonly ConcurrentDictionary<string, ShortUrlEntry> _urls = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Atomically adds a new entry. TryAdd returns false if the key already exists,
    /// preventing duplicate short codes without needing a separate existence check.
    /// </summary>
    public Task<bool> AddAsync(ShortUrlEntry entry)
    {
        return Task.FromResult(_urls.TryAdd(entry.ShortCode, entry));
    }

    /// <summary>Looks up an entry by short code. Returns null if not found.</summary>
    public Task<ShortUrlEntry?> GetByShortCodeAsync(string shortCode)
    {
        _urls.TryGetValue(shortCode, out var entry);
        return Task.FromResult(entry);
    }

    /// <summary>Atomically removes an entry. Returns false if the code wasn't found.</summary>
    public Task<bool> DeleteAsync(string shortCode)
    {
        return Task.FromResult(_urls.TryRemove(shortCode, out _));
    }

    /// <summary>Checks if a short code exists in storage.</summary>
    public Task<bool> ExistsAsync(string shortCode)
    {
        return Task.FromResult(_urls.ContainsKey(shortCode));
    }

    /// <summary>
    /// Atomically increments the click counter using Interlocked.Increment (inside ShortUrlEntry).
    /// Returns 0 if the entry doesn't exist (e.g., deleted between lookup and increment).
    /// </summary>
    public Task<int> IncrementClickCountAsync(string shortCode)
    {
        if (_urls.TryGetValue(shortCode, out var entry))
        {
            return Task.FromResult(entry.IncrementClickCount());
        }
        return Task.FromResult(0);
    }

    /// <summary>Returns a snapshot of all entries. The list is a copy, safe to iterate.</summary>
    public Task<IReadOnlyList<ShortUrlEntry>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<ShortUrlEntry>>(_urls.Values.ToList());
    }

    /// <summary>
    /// Finds all short codes that point to the same long URL.
    /// Used to compute aggregate click counts across multiple aliases for one destination.
    /// </summary>
    public Task<IReadOnlyList<ShortUrlEntry>> GetByLongUrlAsync(string longUrl)
    {
        var results = _urls.Values.Where(e => e.LongUrl == longUrl).ToList();
        return Task.FromResult<IReadOnlyList<ShortUrlEntry>>(results);
    }

    /// <summary>Returns total count of entries matching the optional search filter.</summary>
    public Task<int> GetCountAsync(string? search = null)
    {
        return Task.FromResult(ApplySearch(search).Count());
    }

    /// <summary>
    /// Returns a page of entries sorted by creation date (newest first).
    /// Supports offset-based pagination and optional case-insensitive search on long URL.
    /// </summary>
    public Task<IReadOnlyList<ShortUrlEntry>> GetPagedAsync(int offset, int limit, string? search = null)
    {
        var results = ApplySearch(search)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<ShortUrlEntry>>(results);
    }

    /// <summary>
    /// Applies an optional case-insensitive search filter on the long URL field.
    /// Returns all entries if the search term is null or whitespace.
    /// </summary>
    private IEnumerable<ShortUrlEntry> ApplySearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return _urls.Values;
        }

        return _urls.Values.Where(e =>
            e.LongUrl.Contains(search, StringComparison.OrdinalIgnoreCase));
    }
}
