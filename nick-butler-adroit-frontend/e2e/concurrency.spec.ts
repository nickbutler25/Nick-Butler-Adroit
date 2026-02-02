import { test, expect } from '@playwright/test';

const API_BASE = 'https://localhost:7055/api/urls';
const REDIRECT_BASE = 'https://localhost:7055';

test.describe('Concurrency', () => {
  test('concurrent URL creation with auto-generated codes all succeed', async ({ request }) => {
    const uniqueTag = Date.now().toString();
    const count = 10;

    // Fire off many create requests in parallel, each with a unique long URL
    const responses = await Promise.all(
      Array.from({ length: count }, (_, i) =>
        request.post(API_BASE, {
          data: { longUrl: `https://example.com/concurrent-add-${uniqueTag}-${i}` },
          ignoreHTTPSErrors: true,
        })
      )
    );

    // Every request should succeed with 201
    const shortCodes: string[] = [];
    for (const res of responses) {
      expect(res.status()).toBe(201);
      const body = await res.json();
      expect(body.shortCode).toBeTruthy();
      shortCodes.push(body.shortCode);
    }

    // All generated short codes should be unique
    const uniqueCodes = new Set(shortCodes);
    expect(uniqueCodes.size).toBe(count);
  });

  test('concurrent creation with the same custom code: exactly one wins', async ({ request }) => {
    const alias = 'racec' + Date.now().toString().slice(-5);
    const count = 5;

    // Fire multiple requests for the same custom alias simultaneously
    const responses = await Promise.all(
      Array.from({ length: count }, (_, i) =>
        request.post(API_BASE, {
          data: {
            longUrl: `https://example.com/race-custom-${i}`,
            customCode: alias,
          },
          ignoreHTTPSErrors: true,
        })
      )
    );

    const statuses = responses.map((r) => r.status());
    const created = statuses.filter((s) => s === 201);
    const conflicts = statuses.filter((s) => s === 409);

    // Exactly one request should win with 201, the rest should get 409
    expect(created.length).toBe(1);
    expect(conflicts.length).toBe(count - 1);
  });

  test('concurrent deletes of the same URL: exactly one succeeds', async ({ request }) => {
    const alias = 'delet' + Date.now().toString().slice(-5);

    // Create a URL to delete
    const createRes = await request.post(API_BASE, {
      data: { longUrl: 'https://example.com/concurrent-delete', customCode: alias },
      ignoreHTTPSErrors: true,
    });
    expect(createRes.status()).toBe(201);

    const count = 5;

    // Fire multiple delete requests for the same short code simultaneously
    const responses = await Promise.all(
      Array.from({ length: count }, () =>
        request.delete(`${API_BASE}/${alias}`, { ignoreHTTPSErrors: true })
      )
    );

    const statuses = responses.map((r) => r.status());
    const successes = statuses.filter((s) => s === 200);
    const notFounds = statuses.filter((s) => s === 404);

    // Exactly one delete should succeed, the rest should get 404
    expect(successes.length).toBe(1);
    expect(notFounds.length).toBe(count - 1);
  });

  test('concurrent clicks on the same link: all clicks are counted', async ({ request }) => {
    const alias = 'click' + Date.now().toString().slice(-5);

    // Create a URL to click
    const createRes = await request.post(API_BASE, {
      data: { longUrl: 'https://example.com/concurrent-click', customCode: alias },
      ignoreHTTPSErrors: true,
    });
    expect(createRes.status()).toBe(201);

    const clickCount = 10;

    // Fire many redirect requests in parallel (each increments the click counter)
    const responses = await Promise.all(
      Array.from({ length: clickCount }, () =>
        request.get(`${REDIRECT_BASE}/${alias}`, {
          ignoreHTTPSErrors: true,
          maxRedirects: 0, // Don't follow the 302, just capture it
        })
      )
    );

    // All requests should get a 302 redirect
    for (const res of responses) {
      expect(res.status()).toBe(302);
    }

    // Verify every click was atomically counted
    const statsRes = await request.get(`${API_BASE}/${alias}/stats`, {
      ignoreHTTPSErrors: true,
    });
    expect(statsRes.status()).toBe(200);
    const stats = await statsRes.json();
    expect(stats.clickCount).toBe(clickCount);
  });
});
