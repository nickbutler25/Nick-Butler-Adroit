using NickButlerAdroit.Api.Models;

namespace NickButlerAdroit.Api.Services;

/// <summary>
/// Core service interface for URL shortening business logic.
/// Handles URL creation, resolution, deletion, and statistics retrieval.
/// Implementations are responsible for URL validation, short code generation,
/// click counting, and broadcasting real-time events via SignalR.
/// </summary>
public interface IUrlShortenerService
{
    /// <summary>Returns all shortened URLs with their click statistics.</summary>
    Task<IReadOnlyList<ShortUrlResult>> GetAllAsync();

    /// <summary>Returns a paginated list of URLs, optionally filtered by search term.</summary>
    Task<PagedResult<ShortUrlResult>> GetPagedAsync(int offset, int limit, string? search = null);

    /// <summary>Returns the most recently created URLs, up to <paramref name="count"/>.</summary>
    Task<IReadOnlyList<ShortUrlResult>> GetRecentAsync(int count);

    /// <summary>
    /// Creates a new shortened URL. If <paramref name="customCode"/> is provided, uses it as the
    /// short code (must be unique); otherwise auto-generates a 7-character base62 code.
    /// Validates and normalizes the long URL before storing.
    /// </summary>
    Task<ShortUrlResult> CreateAsync(string longUrl, string? customCode = null);

    /// <summary>
    /// Resolves a short code to its full URL details and increments the click counter.
    /// Used by the API endpoint (returns full metadata).
    /// </summary>
    Task<ShortUrlResult> ResolveAsync(string shortCode);

    /// <summary>
    /// Resolves a short code and returns only the long URL string for HTTP redirects.
    /// Increments click counter and notifies SignalR clients.
    /// Used by the redirect controller for fast 302 responses.
    /// </summary>
    Task<string> ResolveForRedirectAsync(string shortCode);

    /// <summary>Returns click statistics (count and creation date) for a short code.</summary>
    Task<UrlStats> GetStatsAsync(string shortCode);

    /// <summary>Deletes a shortened URL by its short code. Notifies SignalR clients.</summary>
    Task DeleteAsync(string shortCode);
}
