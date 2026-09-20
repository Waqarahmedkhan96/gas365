import { expect, test, type APIRequestContext } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// CSRF tokens are reusable within an identity; refresh after login (tenant changes).
const csrfTokens = new WeakMap<APIRequestContext, Map<string, string>>();

async function post(
  request: APIRequestContext,
  path: string,
  body: unknown,
  tenant?: string,
) {
  const tokens = csrfTokens.get(request) ?? new Map<string, string>();
  csrfTokens.set(request, tokens);
  const identity = tenant ?? 'anonymous';
  if (!tokens.has(identity)) {
    const csrfResponse = await request.get('/api/auth/csrf');
    expect(csrfResponse.ok(), await csrfResponse.text()).toBeTruthy();
    tokens.set(identity, (await csrfResponse.json()).token);
  }
  const response = await request.post(path, {
    data: body,
    headers: {
      'X-CSRF-TOKEN': tokens.get(identity)!,
      ...(tenant ? { 'X-Workspace': tenant } : {}),
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response;
}
test('owner creates catalog and posts, transfers and reverses stock', async ({
  page,
}, info) => {
  test.setTimeout(90000);
  const email = `stock-browser-${info.project.name}-${Date.now()}@test.example`;
  const password = 'Synthetic-Inventory-2026!';
  const request = page.request;
  await post(request, '/api/auth/register', {
    email,
    password,
    name: 'Stock owner',
    businessName: 'Stock workshop',
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
  await page.goto('/workspace/inventory');
  await page
    .getByRole('button', { name: 'Catalog setup', exact: true })
    .click();
  let modal = page.getByRole('dialog', { name: 'Catalog setup' });
  await modal.getByLabel('Unit code').fill('KG');
  await modal.getByRole('button', { name: 'Add unit', exact: true }).click();
  await expect(
    modal.getByText('KG (3 decimals)', { exact: true }),
  ).toBeVisible();
  await modal.getByLabel('Warehouse name').fill('Main store');
  await modal.getByLabel('Location / rack description').fill('Rack A');
  await modal
    .getByRole('button', { name: 'Add warehouse', exact: true })
    .click();
  await expect(
    modal.getByRole('listitem').filter({ hasText: 'Main store' }),
  ).toBeVisible();
  await modal.getByLabel('Warehouse name').fill('Other store');
  await modal
    .getByRole('button', { name: 'Add warehouse', exact: true })
    .click();
  await expect(
    modal.getByRole('listitem').filter({ hasText: 'Other store' }),
  ).toBeVisible();
  await modal.getByRole('button', { name: 'Close dialog' }).click();
  await page.getByRole('button', { name: 'Add item', exact: true }).click();
  modal = page.getByRole('dialog', { name: 'Add catalog item' });
  await modal.getByLabel(/^SKU/).fill('LPG-118');
  await modal.getByLabel(/^Item name/).fill('Domestic LPG');
  await modal.getByLabel('Stock unit').selectOption({ label: 'KG' });
  await modal.getByLabel('Nominal capacity (kg)').fill('11.8');
  await modal.getByRole('button', { name: 'Create item', exact: true }).click();
  await expect(modal).not.toBeVisible();
  await page
    .getByRole('button', { name: 'Domestic LPG', exact: true })
    .filter({ visible: true })
    .click();
  const detail = page.getByRole('region', { name: 'Stock detail' });
  await expect(
    detail.getByText('No stock movements in the active branch.'),
  ).toBeVisible();
  await detail
    .getByRole('button', { name: 'Post stock movement', exact: true })
    .click();
  modal = page.getByRole('dialog', { name: 'Post stock movement' });
  await modal.getByLabel(/^Quantity/).fill('10');
  await modal.getByLabel('Receipt unit cost (PKR)').fill('100');
  await modal
    .getByLabel('Posting reason / opening reconciliation reference')
    .fill('Opening cutover reference 01');
  await modal.getByRole('button', { name: 'Review movement' }).click();
  let firstAttempt = true;
  let commandKey = '';
  await page.route('**/api/workspace/inventory/documents', async (route) => {
    const key = route.request().postDataJSON().idempotencyKey;
    if (firstAttempt) {
      firstAttempt = false;
      commandKey = key;
      const response = await route.fetch();
      expect(response.ok()).toBeTruthy();
      await route.abort('failed'); // The server committed but the browser lost the response.
    } else {
      expect(key).toBe(commandKey);
      await route.continue();
    }
  });
  await modal.getByRole('button', { name: 'Confirm posting' }).click();
  await expect(modal.getByRole('alert')).toBeVisible();
  await modal.getByRole('button', { name: 'Confirm posting' }).click();
  let document = page.getByRole('dialog', { name: 'Stock document' });
  await expect(document.getByText('Opening · Posted')).toBeVisible();
  await page.unroute('**/api/workspace/inventory/documents');
  await document.getByRole('button', { name: 'Close dialog' }).click();
  await expect(detail.getByText('Quantity: 10', { exact: true })).toBeVisible();
  await expect(
    detail.getByRole('button', { name: 'Post stock movement', exact: true }),
  ).toBeFocused();
  await expect(
    detail.getByText('Warehouse value: PKR 1000.00', { exact: true }),
  ).toBeVisible();
  await detail
    .getByRole('button', { name: 'Post stock movement', exact: true })
    .click();
  modal = page.getByRole('dialog', { name: 'Post stock movement' });
  await modal.getByLabel('Movement kind').selectOption('Transfer');
  await modal.getByLabel(/^Quantity/).fill('4');
  await modal
    .getByLabel('Destination warehouse')
    .selectOption({ label: 'Other store' });
  await modal
    .getByLabel('Posting reason / opening reconciliation reference')
    .fill('Replenish other store');
  await modal.getByRole('button', { name: 'Review movement' }).click();
  await modal.getByRole('button', { name: 'Confirm posting' }).click();
  document = page.getByRole('dialog', { name: 'Stock document' });
  await expect(document.getByText('Transfer · Posted')).toBeVisible();
  await document.getByLabel('Reversal reason').fill('Incorrect destination');
  await document.getByRole('button', { name: 'Reverse document' }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: 'Confirm', exact: true })
    .click();
  await expect(document.getByText('Reversal · Posted')).toBeVisible();
  await document.getByRole('button', { name: 'Close dialog' }).click();
  await expect(detail.getByText('Quantity: 10', { exact: true })).toBeVisible();
  await expect(
    detail.getByRole('button', { name: 'Post stock movement', exact: true }),
  ).toBeFocused();
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: info.outputPath('inventory.png'),
    fullPage: true,
  });
});
