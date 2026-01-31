namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Standard error response returned by all API endpoints on failure.
/// </summary>
/// <param name="Error">A human-readable error message describing what went wrong.</param>
public record ErrorResponse(string Error);
