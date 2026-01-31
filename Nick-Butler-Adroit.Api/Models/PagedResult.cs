namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Generic wrapper for paginated API responses.
/// Contains the current page of items and the total count for pagination controls.
/// </summary>
/// <typeparam name="T">The type of items in the page (e.g., ShortUrlResult).</typeparam>
/// <param name="Items">The items in the current page.</param>
/// <param name="TotalCount">Total number of items across all pages (before pagination).</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
