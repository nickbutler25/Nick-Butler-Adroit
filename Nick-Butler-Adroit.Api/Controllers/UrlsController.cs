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
[Produces("application/json")]
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
    /// Returns all shortened URLs with their click statistics.
    /// </summary>
    /// <returns>A list of all shortened URLs.</returns>
    /// <response code="200">Returns the list of shortened URLs.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ShortUrlResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var results = await _service.GetAllAsync();
        return Ok(results);
    }

    /// <summary>
    /// Maximum allowed length for the search query parameter.
    /// Prevents DoS via excessively large search strings that would cause
    /// expensive string.Contains() comparisons against every URL in memory.
    /// 200 characters is generous for any realistic URL substring search.
    /// </summary>
    private const int MaxSearchLength = 200;

    /// <summary>
    /// Returns a paginated list of URLs, optionally filtered by a search term
    /// that matches against the long URL. Limit is clamped to [1, 100].
    /// </summary>
    /// <param name="offset">Zero-based offset into the result set.</param>
    /// <param name="limit">Maximum number of items to return (clamped to 1–100).</param>
    /// <param name="search">Optional search term to filter by long URL (max 200 chars).</param>
    /// <returns>A paged result containing the matching URLs and total count.</returns>
    /// <response code="200">Returns the paginated list of URLs.</response>
    /// <response code="400">The search term exceeds the maximum allowed length.</response>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<ShortUrlResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPaged([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string? search = null)
    {
        // Reject excessively long search terms to prevent CPU-bound DoS.
        // Without this limit, an attacker could send a multi-megabyte search
        // string that gets compared against every stored URL via string.Contains().
        if (search != null && search.Length > MaxSearchLength)
        {
            return BadRequest(new ErrorResponse($"Search term must not exceed {MaxSearchLength} characters."));
        }

        // Clamp pagination parameters to safe ranges
        if (offset < 0) offset = 0;
        if (limit < 1) limit = 1;
        if (limit > 100) limit = 100;

        var result = await _service.GetPagedAsync(offset, limit, search);
        return Ok(result);
    }

    /// <summary>
    /// Returns the most recently created URLs, sorted by creation date descending.
    /// </summary>
    /// <param name="count">Number of recent URLs to return (clamped to 1–100).</param>
    /// <returns>A list of the most recently created URLs.</returns>
    /// <response code="200">Returns the list of recent URLs.</response>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<ShortUrlResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 10)
    {
        if (count < 1) count = 1;
        if (count > 100) count = 100;

        var results = await _service.GetRecentAsync(count);
        return Ok(results);
    }

    /// <summary>
    /// Creates a new shortened URL.
    /// Accepts a long URL and an optional custom short code (alias).
    /// If no custom code is provided, one is auto-generated (7-char base62).
    /// Rate-limited to 20 requests/minute per IP to prevent spam link farms.
    /// </summary>
    /// <param name="request">The long URL and optional custom alias.</param>
    /// <returns>The newly created short URL with metadata.</returns>
    /// <response code="201">Returns the newly created shortened URL.</response>
    /// <response code="400">The URL is invalid or the custom code format is wrong.</response>
    /// <response code="409">The custom short code is already in use.</response>
    /// <response code="429">Rate limit exceeded.</response>
    [HttpPost]
    [Consumes("application/json")]
    [EnableRateLimiting("create")]
    [ProducesResponseType(typeof(ShortUrlResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
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
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (DuplicateShortCodeException ex)
        {
            _logger.LogWarning("Duplicate short code: {Message}", ex.Message);
            return Conflict(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Resolves a short code to its full URL details and increments the click counter.
    /// Used for API-based resolution, not browser redirects.
    /// Rate-limited to 30 requests/minute per IP to prevent scraping.
    /// </summary>
    /// <param name="shortCode">The short code to resolve.</param>
    /// <returns>The resolved URL with click statistics.</returns>
    /// <response code="200">Returns the resolved URL details.</response>
    /// <response code="400">The short code format is invalid.</response>
    /// <response code="404">The short code does not exist.</response>
    /// <response code="429">Rate limit exceeded.</response>
    [HttpGet("{shortCode}")]
    [EnableRateLimiting("resolve")]
    [ProducesResponseType(typeof(ShortUrlResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Resolve(string shortCode)
    {
        try
        {
            var result = await _service.ResolveAsync(shortCode);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Short code not found for resolve: {ShortCode}", shortCode);
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Deletes a shortened URL by its short code.
    /// Notifies connected SignalR clients of the deletion.
    /// Rate-limited to 20 requests/minute per IP (shares the "create" policy).
    /// </summary>
    /// <param name="shortCode">The short code to delete.</param>
    /// <returns>A confirmation message.</returns>
    /// <response code="200">The URL was successfully deleted.</response>
    /// <response code="400">The short code format is invalid.</response>
    /// <response code="404">The short code does not exist.</response>
    /// <response code="429">Rate limit exceeded.</response>
    [HttpDelete("{shortCode}")]
    [EnableRateLimiting("create")]
    [ProducesResponseType(typeof(DeleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Delete(string shortCode)
    {
        try
        {
            await _service.DeleteAsync(shortCode);
            return Ok(new DeleteResponse($"Short code '{shortCode}' deleted."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Short code not found for delete: {ShortCode}", shortCode);
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Returns click statistics for a specific short code,
    /// including total click count and creation timestamp.
    /// </summary>
    /// <param name="shortCode">The short code to get statistics for.</param>
    /// <returns>Click statistics for the short code.</returns>
    /// <response code="200">Returns the click statistics.</response>
    /// <response code="400">The short code format is invalid.</response>
    /// <response code="404">The short code does not exist.</response>
    [HttpGet("{shortCode}/stats")]
    [ProducesResponseType(typeof(UrlStats), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(string shortCode)
    {
        try
        {
            var stats = await _service.GetStatsAsync(shortCode);
            return Ok(stats);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Short code not found for stats: {ShortCode}", shortCode);
            return NotFound(new ErrorResponse(ex.Message));
        }
    }
}
