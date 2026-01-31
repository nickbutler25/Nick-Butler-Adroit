namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Response DTO returned by URL creation, resolution, and listing endpoints.
/// Immutable record type — uses "with" expressions to create modified copies.
/// </summary>
/// <param name="ShortCode">The unique short code (e.g., "aBc1234").</param>
/// <param name="LongUrl">The normalized original URL this short code redirects to.</param>
/// <param name="ClickCount">Number of times this specific short code has been clicked.</param>
/// <param name="LongUrlClickCount">Aggregate clicks across all short codes pointing to the same long URL.</param>
/// <param name="CreatedAt">UTC timestamp of when this short URL was created.</param>
public record ShortUrlResult(string ShortCode, string LongUrl, int ClickCount, int LongUrlClickCount, DateTime CreatedAt);
