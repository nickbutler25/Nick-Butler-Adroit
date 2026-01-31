namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Incoming request DTO for creating a shortened URL.
/// Deserialized from the JSON body of POST /api/urls.
/// </summary>
/// <param name="LongUrl">The original URL to shorten. Must be a valid HTTP/HTTPS URL.</param>
/// <param name="CustomCode">Optional custom alias (5–20 alphanumeric chars). If null, a code is auto-generated.</param>
public record CreateUrlRequest(string LongUrl, string? CustomCode = null);
