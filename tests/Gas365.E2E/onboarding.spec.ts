import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function post(request: APIRequestContext, path: string, body: unknown) {
  const csrf = await (await request.get('/api/auth/csrf')).json();
  const response = await request.post(path, {
    data: body,
    headers: { 'X-CSRF-TOKEN': csrf.token },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}

async function accessible(page: Page) {
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(result.violations).toEqual([]);
}

test('a new owner completes guided setup from the overview', async ({
  page,
}, info) => {
  test.setTimeout(180000);
  const request = page.request;
  const email = `setup-${info.project.name}-${Date.now()}@test.example`;
  const password = 'Synthetic-Setup-2026!';
  await post(request, '/api/auth/register', {
    email,
    password,
    name: 'Setup owner',
    businessName: 'Setup workshop',
  });
  let token = '';
  await expect
    .poll(async () => {
      const inbox = await (
        await request.get('http://127.0.0.1:8025/api/v1/messages')
      ).json();
      const message = inbox.messages.find((m: { To: { Address: string }[] }) =>
        m.To.some((r) => r.Address === email),
      );
      if (!message) return false;
      const body = await (
        await request.get(`http://127.0.0.1:8025/api/v1/message/${message.ID}`)
      ).json();
      token =
        new URL(
          body.Text.match(/http[^\s]+\/confirm[^\s]+/)[0],
        ).searchParams.get('token') || '';
      return !!token;
    })
    .toBe(true);
  await post(request, '/api/auth/confirm', { email, token });
  await post(request, '/api/auth/login', { email, password });

  await page.goto('/workspace');
  await page.getByRole('link', { name: 'Start setup' }).click();
  await expect(
    page.getByRole('heading', { name: 'Business setup', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('list', { name: 'Setup progress' }),
  ).toBeVisible();
  await accessible(page);

  await page.getByLabel('Business name').fill('Setup LPG Traders');
  await page.getByRole('button', { name: 'Save and continue' }).click();
  await expect(page.getByLabel('Store or warehouse name')).toBeVisible();
  await page.getByLabel('Store or warehouse name').fill('Yard store');
  await page.getByRole('button', { name: 'Save and continue' }).click();
  await expect(page.getByLabel(/Cash in the drawer now/)).toBeVisible();
  await page.getByLabel(/Cash in the drawer now/).fill('5000');
  await page.getByRole('button', { name: 'Save and continue' }).click();
  await expect(
    page.getByRole('radiogroup', { name: 'Billing period' }),
  ).toBeVisible();
  await accessible(page);
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Your business is ready' }),
  ).toBeVisible();
  await accessible(page);

  const me = await (await request.get('/api/auth/me')).json();
  const headers = { 'X-Workspace': me.tenantId };
  const tenant = await (
    await request.get('/api/workspace/tenant', { headers })
  ).json();
  expect(tenant.name).toBe('Setup LPG Traders');
  const warehouses = await (
    await request.get('/api/workspace/inventory/warehouses', { headers })
  ).json();
  expect(warehouses.map((w: { name: string }) => w.name)).toContain(
    'Yard store',
  );

  await page.getByRole('button', { name: 'Go to overview' }).click();
  await expect(
    page.getByRole('heading', { name: 'Getting started' }),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Start setup' })).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});
