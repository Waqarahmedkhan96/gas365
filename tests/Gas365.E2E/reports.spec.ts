import { readFile } from 'node:fs/promises';
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

test('owner reviews the dashboard, reads a reconciled report and downloads its export', async ({
  page,
}, info) => {
  test.setTimeout(180000);
  const request = page.request;
  const email = `reports-${info.project.name}-${Date.now()}@test.example`;
  const password = 'Synthetic-Reports-2026!';
  await post(request, '/api/auth/register', {
    email,
    password,
    name: 'Report owner',
    businessName: 'Report workshop',
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
  const me = await (await request.get('/api/auth/me')).json();
  const headers = { 'X-Workspace': me.tenantId };
  const branch = (
    await (await request.get('/api/workspace/branches', { headers })).json()
  )[0];
  const create = async (path: string, body: unknown) =>
    (await post(request, '/api/workspace/' + path, body, me.tenantId)).json();
  const customer = await create('sales/customers', {
    name: 'Report customer',
    phone: '',
    address: '',
  });
  const unit = await create('inventory/units', {
    code: 'KG',
    decimalPlaces: 3,
  });
  const warehouse = await create('inventory/warehouses', {
    branchId: branch.id,
    name: 'Store',
    location: '',
  });
  const item = await create('inventory/items', {
    sku: 'GAS',
    name: 'LPG',
    kind: 'Lpg',
    unitId: unit.id,
    brand: '',
    classification: 'Domestic',
    capacityKg: 11.8,
    tareWeightKg: 12,
    serialTracked: false,
    reorderLevel: 0,
  });
  await create('inventory/documents', {
    idempotencyKey: crypto.randomUUID(),
    kind: 'Opening',
    itemId: item.id,
    warehouseId: warehouse.id,
    bucket: 'Available',
    quantity: 10,
    unitCost: 100,
    reason: 'Opening',
    expectedSequence: 0,
    destinationExpectedSequence: 0,
    serials: [],
  });
  const prices = await create('sales/price-lists', { name: 'Retail' });
  const price = await create(`sales/price-lists/${prices.id}/prices`, {
    itemId: item.id,
    mode: 'Refill',
    unitPrice: 200,
  });
  const draft = await create('sales/drafts', {
    branchId: branch.id,
    customerId: customer.id,
    priceListId: prices.id,
    reference: 'Report invoice',
    notes: '',
    lines: [
      {
        priceId: price.id,
        priceVersion: price.version,
        warehouseId: warehouse.id,
        quantity: 2,
        discount: 0,
      },
    ],
  });
  await create(`sales/drafts/${draft.id}/post`, {
    idempotencyKey: crypto.randomUUID(),
    version: draft.version,
    cashPaid: 150,
    serials: [[]],
  });

  await page.goto('/workspace');
  const figures = page.getByLabel('Key figures');
  await expect(figures.getByText('Net revenue')).toBeVisible();
  await expect(figures.getByText(/400\.00/)).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'Daily trend' }),
  ).toBeVisible();
  await page.getByText('Show daily trend as a table').click();
  await expect(
    page.getByRole('table', { name: 'Daily trend' }).getByText(/400\.00/),
  ).toBeVisible();

  await page.goto('/workspace/reports');
  await page
    .getByRole('combobox', { name: 'Report' })
    .selectOption({ label: 'Daily sales' });
  const table = page.getByRole('table');
  await expect(table.getByRole('cell', { name: branch.name })).toBeVisible();
  await expect(table.getByRole('rowheader', { name: 'Total' })).toBeVisible();
  await expect(
    page.getByText(/Reconciliation: Net revenue equals/),
  ).toBeVisible();
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);

  await page.getByRole('button', { name: 'Export CSV' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'CSV export queued' }),
  ).toBeVisible();
  const downloadButton = page.getByRole('button', {
    name: /Download gas365-daily-sales-/,
  });
  await expect(downloadButton).toBeVisible({ timeout: 30000 });
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    downloadButton.click(),
  ]);
  expect(download.suggestedFilename()).toMatch(
    /^gas365-daily-sales-\d{8}-\d{8}\.csv$/,
  );
  const csv = await readFile((await download.path())!, 'utf8');
  expect(csv).toContain('Net revenue');
  expect(csv).toContain('400');
  expect(csv).toContain('150');
});
