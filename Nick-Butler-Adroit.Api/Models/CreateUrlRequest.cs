using System.ComponentModel.DataAnnotations;

namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Incoming request DTO for creating a shortened URL.
/// Deserialized from the JSON body of POST /api/urls.
///
/// Validation attributes provide early rejection at the model-binding level
/// before the request reaches the service layer. This gives callers clear,
/// structured 400 responses and avoids unnecessary processing of obviously
/// invalid input.
/// </summary>
/// <param name="LongUrl">
/// The original URL to shorten. Must be a valid HTTP/HTTPS URL.
/// Required and limited to 2048 characters to prevent memory abuse
/// from excessively large URLs being stored in the in-memory repository.
/// The 2048-character limit aligns with the practical maximum URL length
/// supported by most browsers and web servers.
/// </param>
/// <param name="CustomCode">
/// Optional custom alias (5-20 alphanumeric chars). If null, a 7-character
/// base62 code is auto-generated. Format constraints are enforced both here
/// via attributes and at the service layer for defense-in-depth.
/// </param>
public record CreateUrlRequest(
    [Required(ErrorMessage = "A URL is required.")]
    [StringLength(2048, ErrorMessage = "URL must not exceed 2048 characters.")]
    string LongUrl,

    [StringLength(20, MinimumLength = 5, ErrorMessage = "Custom code must be between 5 and 20 characters.")]
    [RegularExpression("^[a-zA-Z0-9]+$", ErrorMessage = "Custom code must contain only alphanumeric characters.")]
    string? CustomCode = null
);
