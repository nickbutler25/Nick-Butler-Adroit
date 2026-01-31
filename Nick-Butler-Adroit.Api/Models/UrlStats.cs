namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Response DTO for the GET /api/urls/{shortCode}/stats endpoint.
/// Provides click statistics and creation metadata for a single short code.
/// </summary>
/// <param name="ShortCode">The short code being queried.</param>
/// <param name="ClickCount">Total number of times this short code has been clicked.</param>
/// <param name="CreatedAt">UTC timestamp of when the short URL was created.</param>
public record UrlStats(string ShortCode, int ClickCount, DateTime CreatedAt);
