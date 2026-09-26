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
  test.setTimeout(180000);
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
  const warehouseResponse = await post(
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
  const itemResponse = await post(
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
  const warehouse = await warehouseResponse.json();
  const item = await itemResponse.json();
  await post(
    request,
    stock + '/documents',
    {
      idempotencyKey: crypto.randomUUID(),
      kind: 'Opening',
      itemId: item.id,
      warehouseId: warehouse.id,
      bucket: 'Available',
      quantity: 200,
      unitCost: 100,
      reason: 'Opening for sales journey',
      expectedSequence: 0,
      destinationExpectedSequence: 0,
      serials: [],
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
    .getByLabel('Cylinder reference for line 1', { exact: true })
    .fill('Cylinder A');
  await page
    .getByLabel('Source cylinder reference for line 1', { exact: true })
    .fill('Source 45-A');
  await page.getByLabel('Loss for line 1 (same unit as quantity)').fill('0.1');
  await page.getByLabel('Loss reason for line 1').fill('Measured filling loss');
  await page
    .getByRole('button', { name: 'Save sales draft', exact: true })
    .click();
  await expect(detail).toContainText('1,311.00');
  await expect(detail).toContainText('Loss 0.1 KG');
  await detail.getByLabel('Cash received on sale (PKR)').fill('1000');
  await detail.getByRole('button', { name: 'Post sale', exact: true }).click();
  await expect(detail).toHaveCount(0);
  await page
    .getByRole('region', { name: 'Sales register' })
    .getByRole('button', { name: 'SALE-REVIEW-001', exact: true })
    .filter({ visible: true })
    .click();
  const posted = page.getByRole('region', { name: 'Posted sale details' });
  await expect(posted).toContainText('Partially Paid');
  await expect(posted).toContainText('311.00');
  await expect(posted).toContainText('410.00');
  await expect(posted).toContainText('Source 45-A');
  await posted.getByLabel('Payment amount (PKR)').fill('311');
  await posted
    .getByLabel('Payment reason (required)', { exact: true })
    .fill('Balance received');
  await posted
    .getByRole('button', { name: 'Receive payment', exact: true })
    .click();
  await expect(posted.getByRole('heading', { level: 2 })).toContainText('Paid');
  const download = page.waitForEvent('download');
  await posted.getByRole('button', { name: 'Download sales receipt' }).click();
  expect((await download).suggestedFilename()).toMatch(/Gas365-sale-.*\.pdf/);
  const balances = await (
    await request.get(`${stock}/items/${item.id}/balances`, { headers })
  ).json();
  expect(balances[0].quantity).toBe(195.9);
  const shellUnit = await (
    await post(
      request,
      stock + '/units',
      { code: 'CYL', decimalPlaces: 0 },
      me.tenantId,
    )
  ).json();
  const shell = await (
    await post(
      request,
      stock + '/items',
      {
        sku: 'SHELL-45',
        name: '45 kg cylinder shell',
        kind: 'Cylinder',
        unitId: shellUnit.id,
        categoryId: null,
        brand: 'Gas365',
        classification: 'Commercial',
        capacityKg: 45,
        tareWeightKg: 35,
        serialTracked: false,
        reorderLevel: 0,
      },
      me.tenantId,
    )
  ).json();
  await post(
    request,
    stock + '/documents',
    {
      idempotencyKey: crypto.randomUUID(),
      kind: 'Opening',
      itemId: shell.id,
      warehouseId: warehouse.id,
      bucket: 'Full',
      quantity: 2,
      unitCost: 1000,
      reason: 'Shell stock excludes LPG',
      expectedSequence: 0,
      destinationExpectedSequence: 0,
      serials: [],
    },
    me.tenantId,
  );
  await page
    .getByText('Configure cylinder LPG and deposit', { exact: true })
    .click();
  await page.getByLabel('Find cylinder type').fill('SHELL-45');
  await page
    .getByRole('button', {
      name: 'SHELL-45 — 45 kg cylinder shell',
      exact: true,
    })
    .click();
  const policy = page.getByRole('form', { name: 'Cylinder policy' });
  await policy.getByLabel('Find LPG item (KG unit)').fill('PRICE-LPG');
  await policy
    .getByRole('button', { name: 'PRICE-LPG — Price list LPG', exact: true })
    .click();
  await policy.getByLabel('LPG kg per filled cylinder').fill('45');
  await policy.getByLabel('Suggested deposit per cylinder (PKR)').fill('5000');
  await policy
    .getByLabel('Usage fee terms shown to customer')
    .fill(
      'Usage fee entered on return; damage or loss requires manager approval.',
    );
  await policy
    .getByRole('button', { name: 'Save cylinder policy', exact: true })
    .click();
  await expect
    .poll(
      async () =>
        (
          await (
            await request.get(
              `/api/workspace/sales/cylinder-policies/${shell.id}`,
              { headers },
            )
          ).json()
        )?.suggestedDeposit,
    )
    .toBe(5000);
  const lists = await (
    await request.get('/api/workspace/sales/price-lists', { headers })
  ).json();
  await post(
    request,
    `/api/workspace/sales/price-lists/${lists.items[0].id}/prices`,
    { itemId: shell.id, mode: 'LoanRefill', unitPrice: 9000 },
    me.tenantId,
  );
  await page
    .getByRole('button', { name: 'New sales draft', exact: true })
    .click();
  await page.getByLabel('Sales reference').fill('LOAN-BROWSER-001');
  await draftEditor
    .getByRole('button', { name: 'Customer Beta', exact: true })
    .click();
  await draftEditor
    .getByRole('button', { name: 'Retail', exact: true })
    .click();
  await draftEditor
    .getByRole('button', { name: 'Add SHELL-45 (LoanRefill)', exact: true })
    .click();
  await draftEditor
    .getByRole('button', { name: 'Save sales draft', exact: true })
    .click();
  await detail.getByLabel('Cash received on sale (PKR)').fill('9000');
  await detail
    .getByLabel('Deposit collected per cylinder for line 1 (PKR)')
    .fill('5000');
  await detail.getByRole('button', { name: 'Post sale', exact: true }).click();
  await expect(detail).toHaveCount(0);
  const loans = await (
    await request.get(`/api/workspace/sales/loans?branchId=${branches[0].id}`, {
      headers,
    })
  ).json();
  expect(loans.items).toHaveLength(1);
  await page
    .getByRole('region', { name: 'Borrowed cylinders', exact: true })
    .getByRole('button', {
      name: `Loan ${loans.items[0].id.slice(0, 8)}`,
      exact: true,
    })
    .filter({ visible: true })
    .click();
  const loanDetail = page.getByRole('region', {
    name: 'Cylinder loan details',
  });
  await loanDetail.getByLabel('Usage fee for this return (PKR)').fill('2000');
  await loanDetail
    .getByLabel('Return reason (required)', { exact: true })
    .fill('Cylinder returned empty');
  await loanDetail
    .getByRole('button', { name: 'Record cylinder return', exact: true })
    .click();
  await expect(loanDetail).toContainText('3,000.00');
  await loanDetail.getByLabel('Refund amount (PKR)').fill('3000');
  await loanDetail
    .getByLabel('Refund reference (required)', { exact: true })
    .fill('Cash handed back');
  await loanDetail
    .getByRole('button', { name: 'Record refund cash', exact: true })
    .click();
  await expect(loanDetail.getByLabel('Refund amount (PKR)')).toHaveCount(0);
  const settled = await (
    await request.get(`/api/workspace/sales/loans/${loans.items[0].id}`, {
      headers,
    })
  ).json();
  expect(settled.refundDue).toBe(0);
  expect(
    settled.entries.reduce(
      (sum: number, e: { revenue: number }) => sum + e.revenue,
      0,
    ),
  ).toBe(2000);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});
