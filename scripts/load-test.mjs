// Concurrency and latency test through the web proxy, like real browsers.
// Several businesses each get several signed-in users who repeatedly create and post sales and read reports.
// Usage: node scripts/load-test.mjs [tenants=4] [usersPerTenant=3] [seconds=60]
import { mkdirSync, writeFileSync } from 'node:fs';

const [tenants = 4, usersPerTenant = 3, seconds = 60] = process.argv
  .slice(2)
  .map(Number);
const base = process.env.WEB_URL || 'http://127.0.0.1:8080';
const mailpit = process.env.MAILPIT_URL || 'http://127.0.0.1:8025';
const password = 'Synthetic-Load-2026!';
const samples = {};
let errors = 0;
const errorKinds = {};

class Client {
  cookies = new Map();
  tenant = '';
  async request(path, method = 'GET', body, label) {
    // The token request sets the antiforgery cookie, so it must happen before the cookie header is built.
    const csrf =
      method === 'GET' ? null : (await this.json('/api/auth/csrf')).token;
    const headers = {
      Cookie: [...this.cookies].map(([k, v]) => `${k}=${v}`).join('; '),
    };
    if (this.tenant && path.startsWith('/api/workspace'))
      headers['X-Workspace'] = this.tenant;
    if (csrf) {
      headers['X-CSRF-TOKEN'] = csrf;
      headers['Content-Type'] = 'application/json';
    }
    const started = performance.now();
    const response = await fetch(base + path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const elapsed = performance.now() - started;
    for (const cookie of response.headers.getSetCookie()) {
      const [pair] = cookie.split(';');
      this.cookies.set(
        pair.slice(0, pair.indexOf('=')),
        pair.slice(pair.indexOf('=') + 1),
      );
    }
    if (label) (samples[label] ??= []).push(elapsed);
    if (!response.ok) {
      if (label) {
        errors++;
        errorKinds[`${label} ${response.status}`] =
          (errorKinds[`${label} ${response.status}`] || 0) + 1;
      }
      throw new Error(
        `${method} ${path} → ${response.status} ${await response.text()}`,
      );
    }
    return response.status === 204 ? null : response.json();
  }
  json(path) {
    return this.request(path);
  }
  post(path, body, label) {
    return this.request(path, 'POST', body, label);
  }
}

async function owner(index) {
  const client = new Client();
  const email = `load-${Date.now()}-${index}@test.example`;
  await client.post('/api/auth/register', {
    email,
    password,
    name: `Load owner ${index}`,
    businessName: `Load test ${index}`,
  });
  let token = '';
  for (let attempt = 0; attempt < 30 && !token; attempt++) {
    const inbox = await (await fetch(`${mailpit}/api/v1/messages`)).json();
    const message = inbox.messages.find((m) =>
      m.To.some((r) => r.Address === email),
    );
    if (message) {
      const body = await (
        await fetch(`${mailpit}/api/v1/message/${message.ID}`)
      ).json();
      token = new URL(
        body.Text.match(/http[^\s]+\/confirm[^\s]+/)[0],
      ).searchParams.get('token');
    } else await new Promise((r) => setTimeout(r, 500));
  }
  await client.post('/api/auth/confirm', { email, token });
  await client.post('/api/auth/login', { email, password });
  client.tenant = (await client.json('/api/auth/me')).tenantId;
  const ws = (path, body) => client.post('/api/workspace/' + path, body);
  const branch = (await client.json('/api/workspace/branches'))[0];
  const unit = await ws('inventory/units', { code: 'KG', decimalPlaces: 3 });
  const warehouse = await ws('inventory/warehouses', {
    branchId: branch.id,
    name: 'Store',
    location: '',
  });
  const item = await ws('inventory/items', {
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
  await ws('inventory/documents', {
    idempotencyKey: crypto.randomUUID(),
    kind: 'Opening',
    itemId: item.id,
    warehouseId: warehouse.id,
    bucket: 'Available',
    quantity: 1000000,
    unitCost: 100,
    reason: 'Load opening',
    expectedSequence: 0,
    destinationExpectedSequence: 0,
    serials: [],
  });
  const customer = await ws('sales/customers', {
    name: 'Load customer',
    phone: '',
    address: '',
  });
  const list = await ws('sales/price-lists', { name: 'Retail' });
  const price = await ws(`sales/price-lists/${list.id}/prices`, {
    itemId: item.id,
    mode: 'Refill',
    unitPrice: 200,
  });
  return {
    email,
    branch: branch.id,
    warehouse: warehouse.id,
    customer: customer.id,
    list: list.id,
    price,
  };
}

async function user(setup) {
  const client = new Client();
  await client.post('/api/auth/login', { email: setup.email, password });
  client.tenant = (await client.json('/api/auth/me')).tenantId;
  return client;
}

async function virtualUser(client, setup, until) {
  const today = new Date().toISOString().slice(0, 10);
  while (Date.now() < until) {
    try {
      const draft = await client.post(
        '/api/workspace/sales/drafts',
        {
          branchId: setup.branch,
          customerId: setup.customer,
          priceListId: setup.list,
          reference: 'Load sale',
          notes: '',
          lines: [
            {
              priceId: setup.price.id,
              priceVersion: setup.price.version,
              warehouseId: setup.warehouse,
              quantity: 1.5,
              discount: 0,
            },
          ],
        },
        'create sale draft',
      );
      await client.post(
        `/api/workspace/sales/drafts/${draft.id}/post`,
        {
          idempotencyKey: crypto.randomUUID(),
          version: draft.version,
          cashPaid: 150,
          serials: [[]],
        },
        'post sale',
      );
      await client.request(
        `/api/workspace/reports/daily-sales?from=${today}&to=${today}`,
        'GET',
        undefined,
        'daily sales report',
      );
      await client.request(
        `/api/workspace/reports/dashboard?from=${today}&to=${today}`,
        'GET',
        undefined,
        'dashboard',
      );
      await client.request(
        '/api/workspace/sales/customers?page=1',
        'GET',
        undefined,
        'customer list',
      );
    } catch {
      /* Counted in errors; keep the virtual user running. */
    }
    await new Promise((r) => setTimeout(r, 300));
  }
}

const percentile = (values, p) =>
  values.length
    ? values.slice().sort((a, b) => a - b)[
        Math.min(values.length - 1, Math.floor((p / 100) * values.length))
      ]
    : 0;
console.log(`Seeding ${tenants} businesses with ${usersPerTenant} users each…`);
const setups = [];
for (let i = 0; i < tenants; i++) setups.push(await owner(i));
const clients = [];
for (const setup of setups)
  for (let u = 0; u < usersPerTenant; u++)
    clients.push([await user(setup), setup]);
console.log(
  `Running ${clients.length} concurrent users for ${seconds} s against ${base}…`,
);
const started = Date.now();
await Promise.all(
  clients.map(([client, setup]) =>
    virtualUser(client, setup, started + seconds * 1000),
  ),
);
const duration = (Date.now() - started) / 1000;
const rows = Object.entries(samples).map(([operation, values]) => ({
  operation,
  requests: values.length,
  perSecond: +(values.length / duration).toFixed(1),
  p50: Math.round(percentile(values, 50)),
  p95: Math.round(percentile(values, 95)),
  p99: Math.round(percentile(values, 99)),
  max: Math.round(Math.max(...values)),
}));
const total = rows.reduce((s, r) => s + r.requests, 0);
console.table(rows);
console.log(
  `${total} measured requests in ${duration.toFixed(0)} s (${(total / duration).toFixed(1)}/s), ${errors} errors.`,
  errorKinds,
);
mkdirSync('artifacts', { recursive: true });
writeFileSync(
  'artifacts/load-test.json',
  JSON.stringify(
    {
      tenants,
      usersPerTenant,
      seconds: duration,
      total,
      errors,
      errorKinds,
      rows,
    },
    null,
    2,
  ),
);
if (errors > total * 0.01) {
  console.error('Error rate above 1%.');
  process.exitCode = 1;
}
