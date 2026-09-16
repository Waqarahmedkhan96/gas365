import { expect, test } from '@playwright/test';

test('platform connects through the web proxy and fits the viewport', async ({
  page,
  request,
}) => {
  const response = await request.get('/api/health');
  expect(response.ok()).toBeTruthy();
  expect((await response.json()).database).toBe('Healthy');
  await page.goto('/');
  await expect(page.getByRole('status')).toHaveText('All systems ready');
  await expect(page.getByRole('button', { name: 'Check again' })).toBeEnabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
});

test('a failed API request offers a usable retry', async ({ page }) => {
  await page.route('**/api/health', (route) => route.abort());
  await page.goto('/');
  await expect(page.getByRole('alert')).toBeVisible();
  await page.unroute('**/api/health');
  await page.getByRole('button', { name: 'Check again' }).click();
  await expect(page.getByRole('status')).toHaveText('All systems ready');
});
