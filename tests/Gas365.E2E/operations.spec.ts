import { expect, test, type APIRequestContext } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function post(
  request: APIRequestContext,
  path: string,
  body: unknown,
  tenant?: string,
) {
  const csrf = await (await request.get('/api/auth/csrf')).json();
  const response = await request.post(path, {
    data: body,
    headers: {
      'X-CSRF-TOKEN': csrf.token,
      ...(tenant ? { 'X-Workspace': tenant } : {}),
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response;
}

test('owner imports customers, records an opening balance, receives the overdue alert and reviews audit', async ({
  page,
}, info) => {
  test.setTimeout(240000);
  const request = page.request;
  const email = `operations-${info.project.name}-${Date.now()}@test.example`;
  const password = 'Synthetic-Operations-2026!';
  await post(request, '/api/auth/register', {
    email,
    password,
    name: 'Operations owner',
    businessName: 'Operations workshop',
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
  await page.goto('/workspace/imports');
  await page
    .getByRole('combobox', { name: 'Data to import' })
    .selectOption({ label: 'Customers' });
  await page.getByLabel(/Excel \(\.xlsx\) or CSV file/).setInputFiles({
    name: 'customers.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from(
      'Name,Phone,Address\r\nAli Gas,0300,Karachi\r\nHotel Mehran,0301,Lahore\r\n',
    ),
  });
  await page.getByRole('button', { name: 'Upload and check' }).click();
  await expect(
    page.getByText('Ready to import', { exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Import 2 rows' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'Import finished' }),
  ).toBeVisible();
  await expect(page.getByText('Imported', { exact: true })).toBeVisible();

  await page.goto('/workspace/udhaar');
  await page
    .getByRole('combobox', { name: 'Counterparty' })
    .selectOption({ label: 'Hotel Mehran' });
  await page
    .locator('summary')
    .filter({ hasText: 'Record opening balance' })
    .click();
  const old = new Date(Date.now() - 45 * 86_400_000).toISOString().slice(0, 10);
  await page.getByLabel(/Original invoice date/).fill(old);
  await page.getByLabel(/Opening amount/).fill('900');
  await page.getByLabel(/Reconciliation reason/).fill('Workbook cutover');
  await page.getByRole('button', { name: 'Record opening balance' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'Saved. Balances refreshed.' }),
  ).toBeVisible();
  await expect(
    page.getByRole('cell', { name: 'Opening balance', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('cell', { name: '31–60', exact: true }),
  ).toBeVisible();

  // The API sweeps alerts on an interval; reload until the overdue alert reaches the bell.
  await expect
    .poll(
      async () => {
        await page.goto('/workspace/notifications');
        // Each poll waits briefly for the list to load before concluding the alert is absent.
        return page
          .getByText(/Overdue credit: Hotel Mehran/)
          .waitFor({ timeout: 4000 })
          .then(
            () => 1,
            () => 0,
          );
      },
      { timeout: 60000, intervals: [5000] },
    )
    .toBe(1);
  await expect(
    page.getByRole('link', { name: 'Notifications, 1 unread' }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Mark all as read' }).click();
  await expect(
    page.getByRole('link', { name: 'Notifications, 0 unread' }),
  ).toBeVisible();

  await page.goto('/workspace/audit');
  await expect(page.getByText(/import committed/)).toBeVisible();
  await page.getByRole('button', { name: 'Corrections and reversals' }).click();
  await expect(page.getByText('No corrections in this period.')).toBeVisible();
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);
});
