import { test, expect } from '@playwright/test';

test.describe('Home Page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('page loads with heading and form visible', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Adroit URL Shortener' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Shorten a Link' })).toBeVisible();
    await expect(page.locator('#longUrl')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Shorten Link' })).toBeVisible();
  });

  test('create a shortened URL with auto-generated code', async ({ page }) => {
    const longUrl = 'https://example.com/test-auto-' + Date.now();

    await page.locator('#longUrl').fill(longUrl);
    await page.getByRole('button', { name: 'Shorten Link' }).click();

    // The new URL should appear in the recent links list
    await expect(page.locator('.url-item .short-link').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.url-item .long-url').first()).toContainText('example.com');
  });

  test('create a shortened URL with a custom alias', async ({ page }) => {
    const alias = 'custom' + Date.now().toString().slice(-6);
    const longUrl = 'https://example.com/test-custom-' + Date.now();

    await page.locator('#longUrl').fill(longUrl);
    await page.locator('#customCode').fill(alias);
    await page.getByRole('button', { name: 'Shorten Link' }).click();

    // The new URL with custom alias should appear
    await expect(page.locator('.url-item .short-link').first()).toContainText(alias, { timeout: 10000 });
  });

  test('validation: reject invalid URLs', async ({ page }) => {
    await page.locator('#longUrl').fill('not-a-url');
    await page.getByRole('button', { name: 'Shorten Link' }).click();

    await expect(page.locator('.error')).toContainText('valid URL');
  });

  test('validation: reject too-short custom codes', async ({ page }) => {
    await page.locator('#longUrl').fill('https://example.com/valid');
    await page.locator('#customCode').fill('ab');
    await page.getByRole('button', { name: 'Shorten Link' }).click();

    await expect(page.locator('.error')).toContainText('at least 5 characters');
  });

  test('delete a URL from the recent list', async ({ page }) => {
    const longUrl = 'https://example.com/to-delete-' + Date.now();

    // Create a URL first
    await page.locator('#longUrl').fill(longUrl);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });

    // Remember how many items before delete
    const countBefore = await page.locator('.url-item').count();

    // Click delete on the first item
    await page.locator('.url-item .delete-btn').first().click();

    // The item count should decrease or the list should show empty message
    if (countBefore === 1) {
      await expect(page.locator('.empty-message')).toBeVisible({ timeout: 5000 });
    } else {
      await expect(page.locator('.url-item')).toHaveCount(countBefore - 1, { timeout: 5000 });
    }
  });

  test('click count increments after visiting the redirect link', async ({ page, context }) => {
    const longUrl = 'https://example.com/click-test-' + Date.now();

    // Create a URL
    await page.locator('#longUrl').fill(longUrl);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item .short-link').first()).toBeVisible({ timeout: 10000 });

    // Get the short link href
    const shortLink = page.locator('.url-item .short-link').first();
    const href = await shortLink.getAttribute('href');
    expect(href).toBeTruthy();

    // Get initial click count text
    const statsText = await page.locator('.url-item .url-stats').first().textContent();
    const initialClicks = parseInt(statsText?.match(/Clicks:\s*(\d+)/)?.[1] ?? '0', 10);

    // Visit the redirect link in a new tab (this increments the click count)
    const newPage = await context.newPage();
    // Use the API resolve endpoint which also increments click count,
    // but the actual redirect URL goes through the RedirectController.
    // We navigate to the short URL directly.
    await newPage.goto(href!, { waitUntil: 'commit' }).catch(() => {
      // The redirect target (example.com) may fail to load, that's fine
    });
    await newPage.close();

    // Wait for the click count to update via SignalR or re-check
    await page.waitForTimeout(2000);

    // Re-check the stats - click count should have incremented
    const updatedStats = await page.locator('.url-item .url-stats').first().textContent();
    const updatedClicks = parseInt(updatedStats?.match(/Clicks:\s*(\d+)/)?.[1] ?? '0', 10);
    expect(updatedClicks).toBeGreaterThan(initialClicks);
  });

  test('total for URL aggregates clicks across multiple short codes for the same long URL', async ({ page, context }) => {
    const longUrl = 'https://example.com/shared-target-' + Date.now();
    const alias1 = 'first' + Date.now().toString().slice(-6);
    const alias2 = 'secnd' + Date.now().toString().slice(-6);

    // Create first short URL pointing to longUrl
    await page.locator('#longUrl').fill(longUrl);
    await page.locator('#customCode').fill(alias1);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item .short-link').first()).toContainText(alias1, { timeout: 10000 });

    // Create second short URL pointing to the same longUrl
    await page.locator('#longUrl').fill(longUrl);
    await page.locator('#customCode').fill(alias2);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item .short-link').first()).toContainText(alias2, { timeout: 10000 });

    // Both items should show Total for URL: 0 initially
    const item1 = page.locator(`.url-item:has(.short-link:has-text("${alias1}"))`);
    const item2 = page.locator(`.url-item:has(.short-link:has-text("${alias2}"))`);
    await expect(item1.locator('.url-stats')).toContainText('Total for URL: 0');
    await expect(item2.locator('.url-stats')).toContainText('Total for URL: 0');

    // Visit the first short URL to generate a click
    const href1 = await item1.locator('.short-link').getAttribute('href');
    const newPage = await context.newPage();
    await newPage.goto(href1!, { waitUntil: 'commit' }).catch(() => {});
    await newPage.close();

    // Wait for SignalR to propagate the click update
    await page.waitForTimeout(2000);

    // Both items should show Total for URL: 1 (one click across the shared long URL)
    await expect(item1.locator('.url-stats')).toContainText('Total for URL: 1');
    await expect(item2.locator('.url-stats')).toContainText('Total for URL: 1');

    // Only the clicked short code should have Clicks: 1; the other stays at 0
    await expect(item1.locator('.url-stats')).toContainText('Clicks: 1');
    await expect(item2.locator('.url-stats')).toContainText('Clicks: 0');
  });
});
