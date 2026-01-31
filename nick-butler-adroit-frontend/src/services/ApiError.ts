/**
 * Custom error class for API failures.
 * Wraps HTTP status codes so callers can distinguish between error types
 * (e.g., 404 Not Found vs 409 Conflict vs network errors).
 * A status of 0 indicates a network-level failure (server unreachable).
 */
export class ApiError extends Error {
  /** HTTP status code (0 for network errors where no response was received). */
  public readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = 'ApiError';
  }
}
