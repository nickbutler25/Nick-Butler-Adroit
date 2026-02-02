import { test, expect } from '@playwright/test';

test.describe('All Links Page', () => {
  test('navigate to /all and see heading', async ({ page }) => {
    await page.goto('/all');
    await expect(page.getByRole('heading', { name: 'All Links' })).toBeVisible();
  });

  test('created URLs appear in the list', async ({ page }) => {
    // Create a URL from the home page first
    const longUrl = 'https://example.com/all-links-test-' + Date.now();
    await page.goto('/');
    await page.locator('#longUrl').fill(longUrl);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });

    // Navigate to all links page
    await page.goto('/all');

    // The URL should appear in the list
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.total-count')).toContainText(/\d+ total links/);
  });

  test('search filters the list', async ({ page }) => {
    // Create two URLs with distinct paths
    const uniqueTag = Date.now().toString();
    await page.goto('/');

    await page.locator('#longUrl').fill(`https://example.com/searchable-alpha-${uniqueTag}`);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });

    await page.locator('#longUrl').fill(`https://example.com/searchable-beta-${uniqueTag}`);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    // Wait for the second URL to appear
    await page.waitForTimeout(1000);

    // Navigate to all links page
    await page.goto('/all');
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });

    // Search for one specific URL
    await page.locator('.search-input').fill(`searchable-alpha-${uniqueTag}`);

    // Wait for debounced search to trigger
    await page.waitForTimeout(500);

    // Should show filtered results - at least one match
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });
    // The total count should reflect filtered results
    await expect(page.locator('.total-count')).toContainText('1 total links');
  });

  test('delete a URL from the list', async ({ page }) => {
    // Create a URL with a unique identifier so we can search for it
    const uniqueTag = 'del-' + Date.now();
    const longUrl = `https://example.com/all-delete-test-${uniqueTag}`;
    await page.goto('/');
    await page.locator('#longUrl').fill(longUrl);
    await page.getByRole('button', { name: 'Shorten Link' }).click();
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });

    // Navigate to all links page and search for our specific URL
    await page.goto('/all');
    await page.locator('.search-input').fill(uniqueTag);
    await page.waitForTimeout(500);
    await expect(page.locator('.url-item').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.total-count')).toContainText('1 total links');

    // Delete the item
    await page.locator('.url-item .delete-btn').first().click();

    // Should show empty state after deletion
    await expect(page.locator('.empty-message')).toBeVisible({ timeout: 5000 });
  });
});
