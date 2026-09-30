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
test('finance reconciles advances, expenses, loans and controlled drawer closing', async ({
  page,
}, info) => {
  test.setTimeout(240000);
  const request = page.request;
  const email = `finance-${info.project.name}-${Date.now()}@test.example`;
  const password = 'Synthetic-Finance-2026!';
  await post(request, '/api/auth/register', {
    email,
    password,
    name: 'Finance owner',
    businessName: 'Finance workshop',
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
    name: 'Credit customer',
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
    reference: 'Browser invoice',
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
    cashPaid: 0,
    serials: [[]],
  });
  const saved = async () =>
    expect(
      page
        .getByRole('status')
        .filter({ hasText: 'Saved. Balances refreshed.' }),
    ).toBeVisible();
  await page.goto('/workspace/cash-register');
  await page.locator('summary').filter({ hasText: 'Create account' }).click();
  await page.getByLabel(/Account name/).fill('Main drawer');
  await page.getByLabel('Account type').selectOption('Cash');
  await page.getByLabel(/Reconciled opening balance/).fill('1000');
  await page
    .getByLabel(/Opening reconciliation reason/)
    .fill('Counted opening');
  await page
    .getByRole('button', { name: 'Create account', exact: true })
    .click();
  await saved();
  await page.getByLabel('Cash drawer').selectOption({ label: 'Main drawer' });
  await page.getByLabel(/Counted cash/).fill('1000');
  await page.getByLabel('Session / variance reason').fill('Opening checked');
  await page
    .getByRole('button', { name: 'Open register', exact: true })
    .click();
  await saved();
  await expect(page.getByLabel('Drawer session figures')).toContainText(
    /Status\s*Open/,
  );
  await page.goto('/workspace/expenses');
  await page.getByLabel(/Category name/).fill('Utilities');
  await page.getByRole('button', { name: 'Add category' }).click();
  await saved();
  await page
    .getByLabel('Payment account')
    .selectOption({ label: 'Main drawer' });
  await page
    .getByLabel('Expense category', { exact: true })
    .selectOption({ label: 'Utilities' });
  await page.getByLabel(/^Amount \(PKR\)/).fill('100');
  await page.getByLabel('Transaction reason / reference').fill('Electricity');
  await page.getByRole('button', { name: 'Post transaction' }).click();
  await saved();
  await page.goto('/workspace/loans');
  await page.getByLabel('Transaction type').selectOption('LoanReceived');
  await page
    .getByLabel('Payment account')
    .selectOption({ label: 'Main drawer' });
  await page.getByLabel('Loan counterparty').fill('Lender');
  await page.getByLabel(/^Amount \(PKR\)/).fill('500');
  await page
    .getByLabel('Transaction reason / reference')
    .fill('Principal received');
  await page.getByRole('button', { name: 'Post transaction' }).click();
  await saved();
  await page.getByLabel('Transaction type').selectOption('LoanRepayment');
  await page
    .getByLabel('Outstanding loan')
    .selectOption({ label: 'Lender · Principal received · PKR 500.00' });
  await page.getByLabel(/^Amount \(PKR\)/).fill('200');
  await page
    .getByLabel('Transaction reason / reference')
    .fill('Principal repayment');
  await page.getByRole('button', { name: 'Post transaction' }).click();
  await saved();
  await page.goto('/workspace/udhaar');
  await page
    .getByLabel('Counterparty', { exact: true })
    .selectOption({ label: 'Credit customer' });
  await page
    .getByLabel('Payment account')
    .selectOption({ label: 'Main drawer' });
  await page.getByLabel(/^Amount \(PKR\)/).fill('500');
  await page
    .getByLabel('Transaction reason / reference')
    .fill('Customer advance');
  await page.getByRole('button', { name: 'Post transaction' }).click();
  await saved();
  await page.getByLabel('Transaction type').selectOption('Allocate');
  await page.getByLabel('Unapplied payment').selectOption({ index: 1 });
  await page.getByLabel('Invoice to allocate').selectOption({ index: 1 });
  await page.getByLabel(/^Amount \(PKR\)/).fill('400');
  await page.getByLabel('Transaction reason / reference').fill('Apply advance');
  await page.getByRole('button', { name: 'Post transaction' }).click();
  await saved();
  await expect(page.getByLabel('Credit balance')).toContainText(
    /Balance\s*-?PKR\s100\.00.*Unapplied\s*PKR\s100\.00/,
  );
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);
  await page.goto('/workspace/cash-register');
  await page.getByLabel('Cash drawer').selectOption({ label: 'Main drawer' });
  await expect(page.getByLabel('Drawer session figures')).toContainText(
    /Expected\s*PKR\s1,700\.00/,
  );
  await page.getByLabel(/Counted cash/).fill('1690');
  await page.getByLabel('Session / variance reason').fill('Ten rupees short');
  await page
    .getByRole('button', { name: 'Close register', exact: true })
    .click();
  await saved();
  await expect(page.getByLabel('Drawer session figures')).toContainText(
    /Status\s*Closed/,
  );
  await page
    .getByLabel('Session / variance reason')
    .fill('Approved shortage correction');
  await page
    .getByRole('button', { name: 'Reopen register', exact: true })
    .click();
  await saved();
  await page.getByLabel('Transaction type').selectOption('CashAdjustmentOut');
  await page
    .getByLabel('Payment account')
    .selectOption({ label: 'Main drawer' });
  await page.getByLabel(/^Amount \(PKR\)/).fill('10');
  await page
    .getByLabel('Transaction reason / reference')
    .fill('Approved cash shortage');
  await page.getByRole('button', { name: 'Post transaction' }).click();
  await saved();
  await expect(page.getByLabel('Drawer session figures')).toContainText(
    /Expected\s*PKR\s1,690\.00/,
  );
  await page
    .getByLabel('Session / variance reason')
    .fill('Reconciled final close');
  await page
    .getByRole('button', { name: 'Close register', exact: true })
    .click();
  await saved();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});
