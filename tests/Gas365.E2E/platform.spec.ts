import { expect, test } from '@playwright/test';

test('platform connects through the web proxy and fits the viewport', async ({
  page,
  request,
}) => {
  const response = await request.get('/api/health');
  expect(response.ok()).toBeTruthy();
  expect((await response.json()).database).toBe('Healthy');
  const html = await request.get('/');
  const nonce = (await html.text()).match(
    /name="style-nonce" content="([a-f0-9]{32})"/,
  )?.[1];
  expect(nonce).toBeTruthy();
  expect(html.headers()['content-security-policy']).toContain(
    `'nonce-${nonce}'`,
  );
  expect(html.headers()['content-security-policy']).not.toContain(
    'unsafe-inline',
  );
  expect(html.headers()['cache-control']).toContain('no-cache');
  const nextHtml = await request.get('/', {
    headers: { 'If-Modified-Since': 'Wed, 01 Jan 2031 00:00:00 GMT' },
  });
  expect(nextHtml.status()).toBe(200);
  expect(nextHtml.headers()['content-security-policy']).not.toEqual(
    html.headers()['content-security-policy'],
  );
  await page.goto('/status');
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
  await page.goto('/status');
  await expect(page.getByRole('alert')).toBeVisible();
  await page.unroute('**/api/health');
  await page.getByRole('button', { name: 'Check again' }).click();
  await expect(page.getByRole('status')).toHaveText('All systems ready');
});
