using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NickButlerAdroit.Api.Models;
using NickButlerAdroit.Api.Services;

namespace NickButlerAdroit.Api.Controllers;

/// <summary>
/// Handles the top-level redirect route: GET /{shortCode}.
/// When a user clicks a shortened link, this controller resolves the short code
/// to the original long URL and issues an HTTP 302 redirect. This is the primary
/// public-facing endpoint — every shortened link hit flows through here.
/// Hidden from Swagger/OpenAPI since it's not part of the management API.
/// </summary>
[ApiController]
public class RedirectController : ControllerBase
{
    private readonly IUrlShortenerService _service;
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(IUrlShortenerService service, ILogger<RedirectController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a short code and redirects (302) to the original long URL.
    /// Also increments the click counter and notifies connected clients via SignalR.
    /// Rate-limited to 60 requests/minute to guard against bot traffic and click fraud.
    /// </summary>
    [HttpGet("{shortCode}")]
    [ApiExplorerSettings(IgnoreApi = true)]   // Excluded from Swagger docs
    [EnableRateLimiting("redirect")]
    public async Task<IActionResult> RedirectToLongUrl(string shortCode)
    {
        try
        {
            var longUrl = await _service.ResolveForRedirectAsync(shortCode);
            _logger.LogInformation("Redirect: {ShortCode} -> {LongUrl}", shortCode, longUrl);
            return Redirect(longUrl);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Redirect bad request for '{ShortCode}': {Message}", shortCode, ex.Message);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Redirect not found: '{ShortCode}'", shortCode);
            return NotFound(new ErrorResponse($"Short code '{shortCode}' not found."));
        }
    }
}
