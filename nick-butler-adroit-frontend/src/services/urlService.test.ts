import { vi } from 'vitest';
import { createShortUrl, resolveShortUrl, deleteShortUrl, getUrlStats } from './urlService';
import { ApiError } from './ApiError';

const mockFetch = vi.fn();
global.fetch = mockFetch;

beforeEach(() => {
  mockFetch.mockReset();
});

describe('urlService', () => {
  describe('createShortUrl', () => {
    it('sends POST request and returns result', async () => {
      const result = {
        shortCode: 'abc1234',
        longUrl: 'https://example.com',
        clickCount: 0,
        longUrlClickCount: 0,
        createdAt: '2025-01-01T00:00:00Z',
      };
      mockFetch.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(result),
      });

      const response = await createShortUrl('https://example.com');

      expect(mockFetch).toHaveBeenCalledWith('/api/urls', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ longUrl: 'https://example.com', customCode: undefined }),
      });
      expect(response).toEqual(result);
    });

    it('throws ApiError on error response', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ error: 'Invalid URL format.' }),
      });

      await expect(createShortUrl('bad')).rejects.toThrow('Invalid URL format.');
      await expect(createShortUrl('bad')).rejects.toBeInstanceOf(ApiError);
    });

    it('throws ApiError with status on error response', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 409,
        json: () => Promise.resolve({ error: 'Duplicate code.' }),
      });

      try {
        await createShortUrl('https://example.com', 'dup');
        expect.unreachable('Expected error');
      } catch (err) {
        expect(err).toBeInstanceOf(ApiError);
        expect((err as ApiError).status).toBe(409);
        expect((err as ApiError).message).toBe('Duplicate code.');
      }
    });
  });

  describe('resolveShortUrl', () => {
    it('sends GET request and returns result', async () => {
      const result = {
        shortCode: 'abc1234',
        longUrl: 'https://example.com',
        clickCount: 1,
        longUrlClickCount: 1,
        createdAt: '2025-01-01T00:00:00Z',
      };
      mockFetch.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(result),
      });

      const response = await resolveShortUrl('abc1234');

      expect(mockFetch).toHaveBeenCalledWith('/api/urls/abc1234', undefined);
      expect(response).toEqual(result);
    });
  });

  describe('deleteShortUrl', () => {
    it('sends DELETE request', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve({ message: 'Deleted.' }),
      });

      await deleteShortUrl('abc1234');

      expect(mockFetch).toHaveBeenCalledWith('/api/urls/abc1234', { method: 'DELETE' });
    });

    it('throws ApiError on error response', async () => {
      mockFetch.mockResolvedValue({
        ok: false,
        status: 404,
        json: () => Promise.resolve({ error: 'Not found.' }),
      });

      await expect(deleteShortUrl('missing')).rejects.toThrow('Not found.');
      await expect(deleteShortUrl('missing')).rejects.toBeInstanceOf(ApiError);
    });
  });

  describe('getUrlStats', () => {
    it('sends GET request and returns stats', async () => {
      const stats = {
        shortCode: 'abc1234',
        clickCount: 5,
        createdAt: '2025-01-01T00:00:00Z',
      };
      mockFetch.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(stats),
      });

      const response = await getUrlStats('abc1234');

      expect(mockFetch).toHaveBeenCalledWith('/api/urls/abc1234/stats', undefined);
      expect(response).toEqual(stats);
    });
  });

  describe('network errors', () => {
    it('throws ApiError with status 0 on network failure', async () => {
      mockFetch.mockRejectedValue(new TypeError('Failed to fetch'));

      try {
        await createShortUrl('https://example.com');
        expect.unreachable('Expected error');
      } catch (err) {
        expect(err).toBeInstanceOf(ApiError);
        expect((err as ApiError).status).toBe(0);
        expect((err as ApiError).message).toBe('Network error — server may be unavailable');
      }
    });

    it('throws network error for deleteShortUrl', async () => {
      mockFetch.mockRejectedValue(new TypeError('Failed to fetch'));

      await expect(deleteShortUrl('abc')).rejects.toThrow('Network error');
    });

    it('throws network error for getUrlStats', async () => {
      mockFetch.mockRejectedValue(new TypeError('Failed to fetch'));

      await expect(getUrlStats('abc')).rejects.toThrow('Network error');
    });
  });
});
