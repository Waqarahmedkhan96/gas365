import { expect, test, type APIRequestContext } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function post(
  request: APIRequestContext,
  path: string,
  body: unknown,
  ok = true,
) {
  const csrf = await (await request.get('/api/auth/csrf')).json();
  const response = await request.post(path, {
    data: body,
    headers: { 'X-CSRF-TOKEN': csrf.token },
  });
  if (ok) expect(response.ok(), await response.text()).toBeTruthy();
  return response;
}

// Registers and verifies the account unless it already exists from an earlier run, then signs in.
async function signIn(
  request: APIRequestContext,
  email: string,
  password: string,
  businessName: string,
) {
  if ((await post(request, '/api/auth/login', { email, password }, false)).ok())
    return;
  await post(request, '/api/auth/register', {
    email,
    password,
    name: businessName + ' owner',
    businessName,
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
}

test('owner requests a plan and grants support; the platform admin activates it and views read-only', async ({
  browser,
  page,
}, info) => {
  test.setTimeout(240000);
  const business = `Subscription ${info.project.name} ${Date.now()}`;
  await signIn(
    page.request,
    `subscription-${info.project.name}-${Date.now()}@test.example`,
    'Synthetic-Subscription-2026!',
    business,
  );
  await page.goto('/workspace');
  await expect(page.getByText(/Free trial: 14 days left/)).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'Getting started' }),
  ).toBeVisible();
  await page.goto('/workspace/subscription');
  await page
    .getByLabel('What do you need help with?')
    .fill('Setting up prices');
  await page.getByRole('button', { name: 'Allow support access' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'Support access granted' }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Choose Starter' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'bank transfer' }),
  ).toBeVisible();
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await signIn(
    admin.request,
    'platform-admin@test.example',
    'Synthetic-Platform-2026!',
    'Gas365 platform',
  );
  await admin.goto('/workspace/platform');
  await admin.getByLabel('Search businesses').fill(business);
  const card = admin.getByRole('listitem').filter({ hasText: business });
  await expect(card.getByText(/requested Starter/)).toBeVisible();
  await card.getByText('Record payment and activate').click();
  await card.getByLabel('Amount received (PKR)').fill('2500');
  await card.getByLabel('Payment reference').fill('HBL-4471');
  await card.getByRole('button', { name: 'Activate plan' }).click();
  await expect(
    admin.getByRole('status').filter({ hasText: 'activated' }),
  ).toBeVisible();
  await admin.getByRole('button', { name: 'Support requests' }).click();
  await admin
    .getByRole('listitem')
    .filter({ hasText: business })
    .getByRole('button', { name: 'Open read-only view' })
    .click();
  await expect(admin.getByText(`Support view of ${business}`)).toBeVisible();
  await admin.goto('/workspace/customers');
  await expect(admin.getByText(`Support view of ${business}`)).toBeVisible();
  await admin.getByRole('button', { name: 'Exit support view' }).click();
  await expect(admin.getByText(/Support view of/)).toHaveCount(0);
  await adminContext.close();

  await page.goto('/workspace/subscription');
  await expect(page.getByText(/Starter.*Active.*paid through/)).toBeVisible();
  await expect(page.getByText(/Free trial/)).toHaveCount(0);
});
