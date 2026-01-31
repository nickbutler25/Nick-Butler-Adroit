using NickButlerAdroit.Api.Models;

namespace NickButlerAdroit.Api.Repositories;

/// <summary>
/// Repository interface for URL storage operations.
/// Abstracts the persistence layer so the service layer doesn't depend on a specific
/// storage mechanism. The current implementation uses an in-memory ConcurrentDictionary,
/// but this interface allows swapping to a database-backed implementation later.
/// All methods are async to support future I/O-bound implementations.
/// </summary>
public interface IUrlRepository
{
    /// <summary>Adds a new URL entry. Returns false if the short code already exists (collision).</summary>
    Task<bool> AddAsync(ShortUrlEntry entry);

    /// <summary>Retrieves a URL entry by its short code, or null if not found.</summary>
    Task<ShortUrlEntry?> GetByShortCodeAsync(string shortCode);

    /// <summary>Removes a URL entry by short code. Returns false if the code didn't exist.</summary>
    Task<bool> DeleteAsync(string shortCode);

    /// <summary>Checks whether a short code is already in use.</summary>
    Task<bool> ExistsAsync(string shortCode);

    /// <summary>Atomically increments the click counter for a short code. Returns the new count.</summary>
    Task<int> IncrementClickCountAsync(string shortCode);

    /// <summary>Returns all URL entries (unordered).</summary>
    Task<IReadOnlyList<ShortUrlEntry>> GetAllAsync();

    /// <summary>Returns all entries that map to the given long URL (one long URL can have many short codes).</summary>
    Task<IReadOnlyList<ShortUrlEntry>> GetByLongUrlAsync(string longUrl);

    /// <summary>Returns the total count of entries, optionally filtered by a search term on long URL.</summary>
    Task<int> GetCountAsync(string? search = null);

    /// <summary>Returns a page of entries sorted by creation date (newest first), with optional search filtering.</summary>
    Task<IReadOnlyList<ShortUrlEntry>> GetPagedAsync(int offset, int limit, string? search = null);
}
