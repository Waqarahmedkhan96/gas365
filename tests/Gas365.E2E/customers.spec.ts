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
test('owner manages customer profiles and versioned selling prices', async ({
  page,
}, info) => {
  test.setTimeout(90000);
  const email = `purchase-browser-${info.project.name}-${Date.now()}@test.example`;
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
  const me = await (await request.get('/api/auth/me')).json();

  const headers = { 'X-Workspace': me.tenantId };
  const branches = await (
    await request.get('/api/workspace/branches', { headers })
  ).json();
  await post(
    request,
    '/api/workspace/inventory/warehouses',
    { branchId: branches[0].id, name: 'Sales store', location: '' },
    me.tenantId,
  );
  const stock = '/api/workspace/inventory';
  const unit = await (
    await post(
      request,
      stock + '/units',
      { code: 'KG', decimalPlaces: 3 },
      me.tenantId,
    )
  ).json();
  await post(
    request,
    stock + '/items',
    {
      sku: 'PRICE-LPG',
      name: 'Price list LPG',
      kind: 'Lpg',
      unitId: unit.id,
      categoryId: null,
      brand: 'Gas365',
      classification: 'Domestic',
      capacityKg: 11.8,
      tareWeightKg: null,
      serialTracked: false,
      reorderLevel: 0,
    },
    me.tenantId,
  );
  await page.goto('/workspace/customers');
  await page.getByRole('button', { name: 'Add customer', exact: true }).click();
  await page.getByLabel('Customer name').fill('Customer Alpha');
  await page.getByLabel('Phone', { exact: true }).fill('03001112222');
  await page.getByLabel('Address', { exact: true }).fill('Customer address');
  await page
    .getByRole('button', { name: 'Save customer', exact: true })
    .click();
  await expect(
    page.getByRole('status').filter({ hasText: 'Saved.' }),
  ).toBeVisible();
  await page
    .getByRole('button', { name: 'Customer Alpha', exact: true })
    .filter({ visible: true })
    .click();
  await expect(
    page.getByRole('region', { name: 'Customer profile' }),
  ).toContainText('03001112222');
  await page
    .getByRole('button', { name: 'Edit Customer Alpha', exact: true })
    .filter({ visible: true })
    .click();
  await page.getByLabel('Customer name').fill('Customer Beta');
  await page
    .getByRole('button', { name: 'Save customer', exact: true })
    .click();
  await expect(
    page.getByRole('region', { name: 'Customer profile' }),
  ).toContainText('Customer Beta');
  await page.getByLabel('Search customers').fill('Beta');
  await expect(
    page
      .getByRole('button', { name: 'Customer Beta', exact: true })
      .filter({ visible: true }),
  ).toBeVisible();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.goto('/workspace/prices');
  await page
    .getByRole('button', { name: 'Add price list', exact: true })
    .click();
  await page.getByLabel('Price list name').fill('Retail');
  await page
    .getByRole('button', { name: 'Save price list', exact: true })
    .click();
  await page
    .getByRole('button', { name: 'Retail', exact: true })
    .filter({ visible: true })
    .click();
  await page.getByRole('button', { name: 'Add price', exact: true }).click();
  await page
    .getByRole('button', { name: 'PRICE-LPG — Price list LPG', exact: true })
    .click();
  await page.getByLabel('Sale mode', { exact: true }).selectOption('Refill');
  await page.getByLabel('Unit price (PKR)').fill('325.123456');
  await page.getByRole('button', { name: 'Save price', exact: true }).click();
  await expect(
    page.getByRole('form', { name: 'Sale price editor' }),
  ).toHaveCount(0);
  await expect(
    page.getByRole('region', { name: 'Price list prices' }),
  ).toContainText('325.123456');
  await page
    .getByRole('button', { name: 'Edit price for PRICE-LPG', exact: true })
    .filter({ visible: true })
    .click();
  await page.getByLabel('Unit price (PKR)').fill('330.50');
  await page.getByRole('button', { name: 'Save price', exact: true }).click();
  await expect(
    page.getByRole('region', { name: 'Price list prices' }),
  ).toContainText('330.50');
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);

  await page.goto('/workspace/sales');
  await page
    .getByRole('button', { name: 'New sales draft', exact: true })
    .click();
  await page.getByLabel('Sales reference').fill('SALE-REVIEW-001');
  const draftEditor = page.getByRole('form', { name: 'Sales draft editor' });
  await draftEditor
    .getByRole('button', { name: 'Customer Beta', exact: true })
    .click();
  await draftEditor
    .getByRole('button', { name: 'Retail', exact: true })
    .click();
  await draftEditor
    .getByRole('button', { name: 'Add PRICE-LPG (Refill)', exact: true })
    .click();
  await page.getByLabel('Quantity for line 1').fill('2');
  await page.getByLabel('Discount for line 1 (PKR)').fill('11');
  await draftEditor
    .getByRole('button', { name: 'Add PRICE-LPG (Refill)', exact: true })
    .click();
  await page
    .getByRole('button', { name: 'Save sales draft', exact: true })
    .click();
  const detail = page.getByRole('region', { name: 'Sales draft details' });
  await expect(detail).toContainText('980.50');
  await page
    .getByRole('button', { name: 'Edit SALE-REVIEW-001', exact: true })
    .filter({ visible: true })
    .click();
  await page.getByLabel('Quantity for line 1').fill('3');
  await page
    .getByRole('button', { name: 'Save sales draft', exact: true })
    .click();
  await expect(detail).toContainText('1,311.00');
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});
