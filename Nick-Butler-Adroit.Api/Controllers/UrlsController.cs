using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NickButlerAdroit.Api.Exceptions;
using NickButlerAdroit.Api.Models;
using NickButlerAdroit.Api.Services;

namespace NickButlerAdroit.Api.Controllers;

/// <summary>
/// REST API controller for URL management operations.
/// Routes are mapped under /api/urls. This is the management API used by the frontend,
/// separate from the public redirect endpoint handled by <see cref="RedirectController"/>.
/// Provides CRUD operations for shortened URLs plus statistics and pagination.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UrlsController : ControllerBase
{
    private readonly IUrlShortenerService _service;
    private readonly ILogger<UrlsController> _logger;

    public UrlsController(IUrlShortenerService service, ILogger<UrlsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/urls — Returns all shortened URLs with their click statistics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var results = await _service.GetAllAsync();
        return Ok(results);
    }

    /// <summary>
    /// GET /api/urls/paged?offset=0&amp;limit=50&amp;search=term
    /// Returns a paginated list of URLs, optionally filtered by a search term
    /// that matches against the long URL. Limit is clamped to [1, 100].
    /// Used by the AllLinksPage with virtual scrolling.
    /// </summary>
    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string? search = null)
    {
        // Clamp pagination parameters to safe ranges
        if (offset < 0) offset = 0;
        if (limit < 1) limit = 1;
        if (limit > 100) limit = 100;

        var result = await _service.GetPagedAsync(offset, limit, search);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/urls/recent?count=10 — Returns the most recently created URLs.
    /// Count is clamped to [1, 100]. Used by the HomePage to show recent links.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 10)
    {
        if (count < 1) count = 1;
        if (count > 100) count = 100;

        var results = await _service.GetRecentAsync(count);
        return Ok(results);
    }

    /// <summary>
    /// POST /api/urls — Creates a new shortened URL.
    /// Accepts a long URL and an optional custom short code (alias).
    /// If no custom code is provided, one is auto-generated (7-char base62).
    /// Rate-limited to 20 requests/minute to prevent spam link farms.
    /// Returns 201 Created with the new URL result, or 409 Conflict if the custom code is taken.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("create")]
    public async Task<IActionResult> Create([FromBody] CreateUrlRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request.LongUrl, request.CustomCode);
            // Return 201 with a Location header pointing to the resolve endpoint
            return CreatedAtAction(nameof(Resolve), new { shortCode = result.ShortCode }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid URL request: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (DuplicateShortCodeException ex)
        {
            _logger.LogWarning("Duplicate short code: {Message}", ex.Message);
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/urls/{shortCode} — Resolves a short code to its full URL details.
    /// Also increments the click counter (used for API-based resolution, not browser redirects).
    /// Rate-limited to 20 requests/minute to prevent scraping.
    /// </summary>
    [HttpGet("{shortCode}")]
    [EnableRateLimiting("resolve")]
    public async Task<IActionResult> Resolve(string shortCode)
    {
        try
        {
            var result = await _service.ResolveAsync(shortCode);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Short code not found for resolve: {ShortCode}", shortCode);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/urls/{shortCode} — Deletes a shortened URL by its short code.
    /// Shares the "create" rate limit policy (20 requests/minute).
    /// Notifies connected SignalR clients of the deletion.
    /// </summary>
    [HttpDelete("{shortCode}")]
    [EnableRateLimiting("create")]
    public async Task<IActionResult> Delete(string shortCode)
    {
        try
        {
            await _service.DeleteAsync(shortCode);
            return Ok(new { message = $"Short code '{shortCode}' deleted." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Short code not found for delete: {ShortCode}", shortCode);
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/urls/{shortCode}/stats — Returns click statistics for a specific short code,
    /// including total click count and creation timestamp.
    /// </summary>
    [HttpGet("{shortCode}/stats")]
    public async Task<IActionResult> GetStats(string shortCode)
    {
        try
        {
            var stats = await _service.GetStatsAsync(shortCode);
            return Ok(stats);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Short code not found for stats: {ShortCode}", shortCode);
            return NotFound(new { error = ex.Message });
        }
    }
}
