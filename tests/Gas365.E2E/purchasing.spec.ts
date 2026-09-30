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
test('owner purchases, retries posting, pays, reverses and exports a voucher', async ({
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
    stock + '/warehouses',
    { branchId: branches[0].id, name: 'Purchase store', location: '' },
    me.tenantId,
  );
  await post(
    request,
    stock + '/items',
    {
      sku: 'LPG-TEST',
      name: 'Purchase LPG',
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
  await page.goto('/workspace/suppliers');
  await page.getByRole('button', { name: 'Add supplier', exact: true }).click();
  await page.getByLabel('Supplier name').fill('Purchase supplier');
  await page.getByLabel('Phone', { exact: true }).fill('03000000000');
  await page
    .getByRole('button', { name: 'Save supplier', exact: true })
    .click();
  await expect(
    page
      .getByRole('button', { name: 'Purchase supplier', exact: true })
      .filter({ visible: true }),
  ).toBeVisible();
  await page.goto('/workspace/purchases');
  await page.getByRole('button', { name: 'New purchase', exact: true }).click();
  const form = page.getByRole('form', { name: 'Purchase draft' });
  await form
    .getByLabel('Supplier', { exact: true })
    .selectOption({ label: 'Purchase supplier' });
  await form.getByLabel('Supplier reference').fill('PUR-BROWSER-001');
  await form.getByLabel('Transport/loading charges (PKR)').fill('60');
  await form
    .getByLabel('Item 1', { exact: true })
    .selectOption({ label: 'LPG-TEST - Purchase LPG' });
  await form
    .getByLabel('Warehouse 1', { exact: true })
    .selectOption({ label: 'Purchase store' });
  await form.getByLabel('Quantity 1').fill('10');
  await form.getByLabel('Unit price 1 (PKR)').fill('100');
  await form.getByRole('button', { name: 'Add line', exact: true }).click();
  await form
    .getByLabel('Item 2', { exact: true })
    .selectOption({ label: 'LPG-TEST - Purchase LPG' });
  await form
    .getByLabel('Warehouse 2', { exact: true })
    .selectOption({ label: 'Purchase store' });
  await form.getByLabel('Quantity 2').fill('20');
  await form.getByLabel('Unit price 2 (PKR)').fill('200');
  await form.getByRole('button', { name: 'Save draft', exact: true }).click();
  const detail = page.getByRole('region', { name: 'Purchase details' });
  await expect(detail.getByText('Draft', { exact: true })).toBeVisible();
  await detail.getByLabel('Cash paid on posting (PKR)').fill('1000');
  await detail.getByLabel('Confirm permanent posting').check();
  let key = '';
  let lost = true;
  await page.route('**/purchasing/purchases/*/post', async (route) => {
    const current = route.request().postDataJSON().idempotencyKey;
    if (lost) {
      lost = false;
      key = current;
      const result = await route.fetch();
      expect(result.ok()).toBeTruthy();
      await route.abort('failed');
    } else {
      expect(current).toBe(key);
      await route.continue();
    }
  });
  await detail
    .getByRole('button', { name: 'Post purchase', exact: true })
    .click();
  await expect(page.getByRole('alert')).toBeVisible();
  await detail
    .getByRole('button', { name: 'Post purchase', exact: true })
    .click();
  await expect(
    detail.getByText('Partially Paid', { exact: true }),
  ).toBeVisible();
  await detail.getByLabel('Payment amount (PKR)').fill('60');
  await detail.getByLabel('Payment reference or reason').fill('Second payment');
  await detail
    .getByRole('button', { name: 'Record payment', exact: true })
    .click();
  await expect(
    detail.getByText('Second payment', { exact: true }),
  ).toBeVisible();
  await detail.getByLabel('Purchase cost payment (PKR)').fill('60');
  await detail
    .getByLabel('Cost type, recipient and reference')
    .fill('Petrol and loading paid separately');
  await detail
    .getByRole('button', { name: 'Record cost payment', exact: true })
    .click();
  await expect(
    detail.getByText('Petrol and loading paid separately', { exact: true }),
  ).toBeVisible();
  const downloadEvent = page.waitForEvent('download');
  await detail
    .getByRole('button', { name: 'Download PDF voucher', exact: true })
    .click();
  const download = await downloadEvent;
  expect(download.suggestedFilename()).toMatch(/Gas365-purchase-.*\.pdf/);
  await download.saveAs(info.outputPath('purchase-voucher.pdf'));
  const violations = (await new AxeBuilder({ page }).analyze()).violations;
  expect(violations).toEqual([]);
  await page.screenshot({
    path: info.outputPath('purchase-details.png'),
    fullPage: true,
  });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  for (let i = 0; i < 3; i++) {
    await detail
      .getByLabel('Reverse payment reason')
      .first()
      .fill('Cash returned for correction');
    await detail.getByLabel('Confirm reverse payment').first().check();
    await detail
      .getByRole('button', { name: 'Reverse payment', exact: true })
      .first()
      .click();
    await expect(
      detail.getByRole('button', { name: 'Reverse payment', exact: true }),
    ).toHaveCount(2 - i);
  }
  await detail
    .getByLabel('Reverse purchase reason')
    .fill('Supplier document correction');
  await detail.getByLabel('Confirm reverse purchase').check();
  await detail
    .getByRole('button', { name: 'Reverse purchase', exact: true })
    .click();
  await expect(detail.getByText('Reversed', { exact: true })).toBeVisible();
  await detail
    .getByRole('button', { name: 'Create replacement draft', exact: true })
    .click();
  await form.getByLabel('Supplier reference').fill('PUR-BROWSER-CORRECTED');
  await form.getByRole('button', { name: 'Save draft', exact: true }).click();
  await expect(
    detail.getByRole('heading', { name: 'Purchase PUR-BROWSER-CORRECTED' }),
  ).toBeVisible();
  await page.goto('/workspace/suppliers');
  await page
    .getByRole('button', { name: 'Purchase supplier', exact: true })
    .filter({ visible: true })
    .click();
  await expect(page.getByLabel('Supplier balance')).toContainText(
    /Payable in selected branch\s*PKR\s0\.00/,
  );
});
