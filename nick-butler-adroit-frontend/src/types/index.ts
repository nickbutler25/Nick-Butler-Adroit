/**
 * Shared TypeScript interfaces matching the C# backend DTOs.
 * These types are used across the API service layer and all React components.
 */

/** Request body for POST /api/urls */
export interface CreateUrlRequest {
  /** The original URL to shorten (must be HTTP/HTTPS). */
  longUrl: string;
  /** Optional custom alias (5-20 alphanumeric chars). Auto-generated if omitted. */
  customCode?: string;
}

/** Response from URL creation, resolution, and listing endpoints. */
export interface ShortUrlResult {
  /** The unique short code (e.g., "aBc1234"). */
  shortCode: string;
  /** The normalized destination URL. */
  longUrl: string;
  /** Click count for this specific short code. */
  clickCount: number;
  /** Aggregate clicks across all short codes pointing to the same long URL. */
  longUrlClickCount: number;
  /** ISO 8601 timestamp of when this short URL was created. */
  createdAt: string;
}

/** Response from the GET /api/urls/{shortCode}/stats endpoint. */
export interface UrlStats {
  shortCode: string;
  clickCount: number;
  createdAt: string;
}

/** Generic paginated response wrapper used with the /api/urls/paged endpoint. */
export interface PagedResult<T> {
  /** Items in the current page. */
  items: T[];
  /** Total count across all pages, for pagination controls. */
  totalCount: number;
}
