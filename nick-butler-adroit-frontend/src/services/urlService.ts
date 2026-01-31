/**
 * REST API client for the Nick-Butler-Adroit backend.
 * All functions call the /api/urls endpoints and return typed responses.
 * Network and HTTP errors are wrapped in ApiError for consistent handling.
 */
import { CreateUrlRequest, PagedResult, ShortUrlResult, UrlStats } from '../types';
import { ApiError } from './ApiError';

/** Base path for all URL management API endpoints. */
const API_BASE = '/api/urls';

/** Backend origin (protocol + host + port). Used to construct full short-link URLs for display. */
const API_ORIGIN = import.meta.env.VITE_API_URL || 'https://localhost:7055';

/**
 * Extracts the host portion from the API origin for display in the domain dropdown.
 * Falls back to 'localhost:7055' if the URL parsing fails.
 */
export function getShortLinkDomain(): string {
  try {
    return new URL(API_ORIGIN).host;
  } catch {
    return 'localhost:7055';
  }
}

/** Constructs the full clickable short-link URL (e.g., "https://localhost:7055/aBc1234"). */
export function formatShortLink(shortCode: string): string {
  return `${API_ORIGIN}/${shortCode}`;
}

/**
 * Parses an API response, throwing an ApiError on non-2xx status codes.
 * Attempts to extract a server-provided error message from the JSON body;
 * falls back to the HTTP status text if parsing fails.
 */
async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => ({ error: 'Request failed' }));
    console.warn(`[API] ${response.url} responded with ${response.status}:`, body.error || `HTTP ${response.status}`);
    throw new ApiError(response.status, body.error || `HTTP ${response.status}`);
  }
  return response.json();
}

/**
 * Wrapper around fetch() that catches network-level errors (e.g., server down,
 * DNS failure, CORS block) and converts them to an ApiError with status 0.
 */
async function safeFetch(input: RequestInfo, init?: RequestInit): Promise<Response> {
  try {
    return await fetch(input, init);
  } catch (err) {
    console.error('[API] Network error:', err);
    throw new ApiError(0, 'Network error — server may be unavailable');
  }
}

/** GET /api/urls — Fetches all shortened URLs. */
export async function getAllUrls(): Promise<ShortUrlResult[]> {
  const response = await safeFetch(API_BASE);
  return handleResponse<ShortUrlResult[]>(response);
}

/**
 * POST /api/urls — Creates a new shortened URL.
 * Sends the long URL and optional custom code as a JSON body.
 */
export async function createShortUrl(longUrl: string, customCode?: string): Promise<ShortUrlResult> {
  const request: CreateUrlRequest = { longUrl, customCode: customCode || undefined };
  const response = await safeFetch(API_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return handleResponse<ShortUrlResult>(response);
}

/** GET /api/urls/{shortCode} — Resolves a short code to its full URL details (also increments click count). */
export async function resolveShortUrl(shortCode: string): Promise<ShortUrlResult> {
  const response = await safeFetch(`${API_BASE}/${shortCode}`);
  return handleResponse<ShortUrlResult>(response);
}

/** DELETE /api/urls/{shortCode} — Deletes a shortened URL. */
export async function deleteShortUrl(shortCode: string): Promise<void> {
  const response = await safeFetch(`${API_BASE}/${shortCode}`, { method: 'DELETE' });
  await handleResponse<{ message: string }>(response);
}

/** GET /api/urls/{shortCode}/stats — Fetches click statistics for a specific short code. */
export async function getUrlStats(shortCode: string): Promise<UrlStats> {
  const response = await safeFetch(`${API_BASE}/${shortCode}/stats`);
  return handleResponse<UrlStats>(response);
}

/** GET /api/urls/recent?count=N — Fetches the N most recently created URLs. */
export async function getRecentUrls(count: number): Promise<ShortUrlResult[]> {
  const response = await safeFetch(`${API_BASE}/recent?count=${count}`);
  return handleResponse<ShortUrlResult[]>(response);
}

/**
 * GET /api/urls/paged?offset=N&limit=N&search=term
 * Fetches a page of URLs with optional search filtering.
 * Used by the AllLinksPage for infinite-scroll pagination.
 */
export async function getPagedUrls(offset: number, limit: number, search?: string): Promise<PagedResult<ShortUrlResult>> {
  const params = new URLSearchParams({ offset: String(offset), limit: String(limit) });
  if (search) params.set('search', search);
  const response = await safeFetch(`${API_BASE}/paged?${params}`);
  return handleResponse<PagedResult<ShortUrlResult>>(response);
}
