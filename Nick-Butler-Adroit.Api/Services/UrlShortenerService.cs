using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NickButlerAdroit.Api.Exceptions;
using NickButlerAdroit.Api.Hubs;
using NickButlerAdroit.Api.Models;
using NickButlerAdroit.Api.Repositories;

namespace NickButlerAdroit.Api.Services;

/// <summary>
/// Core business logic for URL shortening operations.
/// Handles URL validation/normalization, short code generation, click tracking,
/// and real-time event broadcasting via SignalR. Marked as partial to support
/// the source-generated regex (<see cref="CustomCodeRegex"/>).
/// </summary>
public partial class UrlShortenerService(IUrlRepository repository, IHubContext<UrlHub> hubContext, ILogger<UrlShortenerService> logger) : IUrlShortenerService
{
    /// <summary>Length of auto-generated short codes (62^7 ≈ 3.5 trillion combinations).</summary>
    private const int GeneratedCodeLength = 7;

    /// <summary>Base62 character set used for random short code generation.</summary>
    private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly IUrlRepository _repository = repository;
    private readonly IHubContext<UrlHub> _hubContext = hubContext;
    private readonly ILogger<UrlShortenerService> _logger = logger;

    /// <summary>
    /// Fire-and-forget SignalR notification to all connected clients.
    /// Failures are logged but do not propagate — a failed notification should never
    /// break a URL operation. Uses ContinueWith instead of await to avoid blocking.
    /// </summary>
    private void NotifyClients(string eventName, params object[] args)
    {
        _ = _hubContext.Clients.All.SendCoreAsync(eventName, args)
            .ContinueWith(t => _logger.LogWarning(t.Exception, "Failed to send SignalR notification: {Event}", eventName),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>Returns all URLs, each enriched with aggregate long-URL click counts.</summary>
    public async Task<IReadOnlyList<ShortUrlResult>> GetAllAsync()
    {
        var entries = await _repository.GetAllAsync();
        var results = new List<ShortUrlResult>();
        foreach (var entry in entries)
        {
            results.Add(await ToResultAsync(entry));
        }
        return results;
    }

    /// <summary>
    /// Returns a paginated, optionally filtered list of URLs.
    /// Total count is fetched separately to support pagination controls in the frontend.
    /// </summary>
    public async Task<PagedResult<ShortUrlResult>> GetPagedAsync(int offset, int limit, string? search = null)
    {
        var totalCount = await _repository.GetCountAsync(search);
        var entries = await _repository.GetPagedAsync(offset, limit, search);
        var items = new List<ShortUrlResult>();
        foreach (var entry in entries)
        {
            items.Add(await ToResultAsync(entry));
        }
        return new PagedResult<ShortUrlResult>(items, totalCount);
    }

    /// <summary>Returns the N most recently created URLs (sorted by CreatedAt descending).</summary>
    public async Task<IReadOnlyList<ShortUrlResult>> GetRecentAsync(int count)
    {
        var entries = await _repository.GetPagedAsync(0, count);
        var results = new List<ShortUrlResult>();
        foreach (var entry in entries)
        {
            results.Add(await ToResultAsync(entry));
        }
        return results;
    }

    /// <summary>
    /// Creates a new shortened URL. If a custom code is provided, validates and uses it directly.
    /// For auto-generated codes, retries up to 5 times on collision (unlikely with 62^7 space).
    /// The long URL is validated (HTTP/HTTPS only) and normalized before storage to prevent
    /// duplicate entries caused by case, trailing slashes, or default ports.
    /// </summary>
    public async Task<ShortUrlResult> CreateAsync(string longUrl, string? customCode = null)
    {
        var normalizedUrl = ValidateAndNormalizeUrl(longUrl);

        // Custom code path: user provides their own alias
        if (customCode is not null)
        {
            ValidateCustomCode(customCode);
            var entry = new ShortUrlEntry(customCode, normalizedUrl);
            if (!await _repository.AddAsync(entry))
            {
                throw new DuplicateShortCodeException(customCode);
            }
            var result = await ToResultAsync(entry);
            _logger.LogInformation("URL created with custom code: {ShortCode} -> {LongUrl}", customCode, normalizedUrl);
            NotifyClients("UrlCreated", result);
            return result;
        }

        // Auto-generated code path: retry on collision (ConcurrentDictionary.TryAdd returns false)
        const int maxRetries = 5;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var shortCode = GenerateShortCode();
            var entry = new ShortUrlEntry(shortCode, normalizedUrl);
            if (await _repository.AddAsync(entry))
            {
                var result = await ToResultAsync(entry);
                _logger.LogInformation("URL created: {ShortCode} -> {LongUrl}", shortCode, normalizedUrl);
                NotifyClients("UrlCreated", result);
                return result;
            }
            _logger.LogWarning("Auto-generated code collision on attempt {Attempt}: {ShortCode}", attempt + 1, shortCode);
        }

        _logger.LogError("Failed to generate unique short code after {MaxRetries} attempts for URL: {LongUrl}", maxRetries, normalizedUrl);
        throw new DuplicateShortCodeException(
            $"Failed to generate a unique short code after {maxRetries} attempts.");
    }

    /// <summary>
    /// Resolves a short code via the API — returns full metadata and increments click count.
    /// Since IncrementClickCountAsync mutates the entry in-place (reference type in the
    /// ConcurrentDictionary), the subsequent ToResultAsync call reads the already-updated count.
    /// </summary>
    public async Task<ShortUrlResult> ResolveAsync(string shortCode)
    {
        ValidateShortCode(shortCode);

        var entry = await _repository.GetByShortCodeAsync(shortCode) ?? throw new KeyNotFoundException($"Short code '{shortCode}' not found.");
        await _repository.IncrementClickCountAsync(shortCode);
        var result = await ToResultAsync(entry);
        NotifyClients("UrlClicked", result.ShortCode, result.ClickCount, result.LongUrl, result.LongUrlClickCount);
        return result;
    }

    /// <summary>
    /// Resolves a short code for the redirect controller — returns only the long URL string
    /// for a fast 302 response. Increments click count and computes aggregate long-URL clicks
    /// across all short codes pointing to the same destination.
    /// </summary>
    public async Task<string> ResolveForRedirectAsync(string shortCode)
    {
        ValidateShortCode(shortCode);

        var entry = await _repository.GetByShortCodeAsync(shortCode) ?? throw new KeyNotFoundException($"Short code '{shortCode}' not found.");
        var newClickCount = await _repository.IncrementClickCountAsync(shortCode);
        // Aggregate clicks across all short codes that point to the same long URL.
        // The increment above already mutated the entry in-place (ShortUrlEntry is a
        // reference type in the ConcurrentDictionary), so the sum already includes
        // the new click — no need to add 1.
        var allEntries = await _repository.GetByLongUrlAsync(entry.LongUrl);
        var longUrlClickCount = allEntries.Sum(e => e.ClickCount);
        NotifyClients("UrlClicked", shortCode, newClickCount, entry.LongUrl, longUrlClickCount);
        return entry.LongUrl;
    }

    /// <summary>Returns click statistics for a specific short code.</summary>
    public async Task<UrlStats> GetStatsAsync(string shortCode)
    {
        ValidateShortCode(shortCode);

        var entry = await _repository.GetByShortCodeAsync(shortCode);
        return entry is null
            ? throw new KeyNotFoundException($"Short code '{shortCode}' not found.")
            : new UrlStats(entry.ShortCode, entry.ClickCount, entry.CreatedAt);
    }

    /// <summary>
    /// Deletes a shortened URL and broadcasts a UrlDeleted event to all SignalR clients.
    /// </summary>
    public async Task DeleteAsync(string shortCode)
    {
        ValidateShortCode(shortCode);

        if (!await _repository.DeleteAsync(shortCode))
        {
            throw new KeyNotFoundException($"Short code '{shortCode}' not found.");
        }

        _logger.LogInformation("URL deleted: {ShortCode}", shortCode);
        NotifyClients("UrlDeleted", shortCode);
    }

    /// <summary>
    /// Maximum allowed length for a long URL. Matches the model-level [StringLength]
    /// attribute on <see cref="Models.CreateUrlRequest.LongUrl"/>.
    /// This acts as a defense-in-depth check at the service layer in case the
    /// service is called directly (e.g., from tests or future internal callers)
    /// bypassing controller model validation. The 2048-character limit aligns with
    /// the practical maximum URL length supported by most browsers and web servers.
    /// </summary>
    private const int MaxUrlLength = 2048;

    /// <summary>
    /// Validates that the URL is well-formed and uses HTTP or HTTPS scheme.
    /// Rejects non-HTTP schemes (ftp://, javascript:, data:, etc.) to prevent
    /// open redirect abuse and phishing attacks. Also enforces a maximum length
    /// to prevent memory exhaustion from storing excessively large URLs.
    /// Normalizes the URL before returning.
    /// </summary>
    private static string ValidateAndNormalizeUrl(string url)
    {
        // Guard against null/empty input — provides a clear error message instead
        // of the generic "Invalid URL format" that Uri.TryCreate would produce.
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("A URL is required.");
        }

        // Enforce maximum length before parsing to avoid allocating a large Uri
        // object for URLs that will be rejected anyway. Without this check,
        // Uri.TryCreate will happily parse multi-megabyte query strings.
        if (url.Length > MaxUrlLength)
        {
            throw new ArgumentException($"URL must not exceed {MaxUrlLength} characters.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Invalid URL format.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs are allowed.");
        }

        return NormalizeUrl(uri);
    }

    /// <summary>
    /// Normalizes a URL to a canonical form to prevent duplicate entries.
    /// - Lowercases scheme and host (RFC 3986 §3.1, §3.2.2)
    /// - Strips default ports (80 for HTTP, 443 for HTTPS)
    /// - Removes trailing slashes from paths
    /// - Preserves query strings and fragments as-is
    /// </summary>
    private static string NormalizeUrl(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();

        // Only include port if it's non-default for the scheme
        var includePort = uri.Port != -1 &&
            !((scheme == "http" && uri.Port == 80) ||
              (scheme == "https" && uri.Port == 443));

        // Strip trailing slashes (but keep root "/" as empty string)
        var path = uri.AbsolutePath;
        if (path.EndsWith('/') && path.Length > 1)
        {
            path = path.TrimEnd('/');
        }
        else if (path == "/")
        {
            path = "";
        }

        var portPart = includePort ? $":{uri.Port}" : "";
        var queryPart = string.IsNullOrEmpty(uri.Query) ? "" : uri.Query;
        var fragmentPart = string.IsNullOrEmpty(uri.Fragment) ? "" : uri.Fragment;

        return $"{scheme}://{host}{portPart}{path}{queryPart}{fragmentPart}";
    }

    /// <summary>
    /// Validates a user-provided custom short code (alias).
    /// Must be 5–20 characters, alphanumeric only — prevents injection and ensures URL-safety.
    /// </summary>
    private static void ValidateCustomCode(string code)
    {
        if (code.Length < 5 || code.Length > 20)
        {
            throw new ArgumentException("Custom code must be between 5 and 20 characters.");
        }

        if (!CustomCodeRegex().IsMatch(code))
        {
            throw new ArgumentException("Custom code must contain only alphanumeric characters.");
        }
    }

    /// <summary>
    /// Validates any short code (custom or auto-generated) before use in lookups.
    /// Ensures it's non-empty, not too long, and alphanumeric to prevent injection.
    /// </summary>
    private static void ValidateShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            throw new ArgumentException("Short code is required.");
        }

        if (shortCode.Length > 20)
        {
            throw new ArgumentException("Short code is too long.");
        }

        if (!CustomCodeRegex().IsMatch(shortCode))
        {
            throw new ArgumentException("Invalid short code format.");
        }
    }

    /// <summary>
    /// Generates a random 7-character base62 short code using <see cref="Random.Shared"/>
    /// (thread-safe). Uses string.Create for zero-allocation string building.
    /// With 62^7 ≈ 3.5 trillion combinations, collision probability is negligible
    /// for in-memory storage scenarios.
    /// </summary>
    private static string GenerateShortCode()
    {
        return string.Create(GeneratedCodeLength, Random.Shared, (span, random) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = AlphanumericChars[random.Next(AlphanumericChars.Length)];
            }
        });
    }

    /// <summary>
    /// Converts a storage entry to an API result, enriching it with the aggregate
    /// click count across all short codes pointing to the same long URL.
    /// This allows the frontend to show "total clicks for this destination."
    /// </summary>
    private async Task<ShortUrlResult> ToResultAsync(ShortUrlEntry entry)
    {
        var allEntries = await _repository.GetByLongUrlAsync(entry.LongUrl);
        var longUrlClickCount = allEntries.Sum(e => e.ClickCount);
        return new ShortUrlResult(entry.ShortCode, entry.LongUrl, entry.ClickCount, longUrlClickCount, entry.CreatedAt);
    }

    /// <summary>Source-generated regex for validating alphanumeric-only strings.</summary>
    [GeneratedRegex("^[a-zA-Z0-9]+$")]
    private static partial Regex CustomCodeRegex();
}
