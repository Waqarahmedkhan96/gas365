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

// Every page an owner can reach, audited on desktop and mobile. Pages load empty data, which exercises
// their empty states; data-heavy tables are covered by the journey specs.
const pages = [
  'workspace',
  'workspace/notifications',
  'workspace/imports',
  'workspace/reports',
  'workspace/udhaar',
  'workspace/expenses',
  'workspace/loans',
  'workspace/cash-register',
  'workspace/sales',
  'workspace/customers',
  'workspace/prices',
  'workspace/purchases',
  'workspace/suppliers',
  'workspace/inventory',
  'workspace/branches',
  'workspace/team',
  'workspace/roles',
  'workspace/settings',
  'workspace/sessions',
  'workspace/audit',
  'workspace/subscription',
  'workspace/showcase',
];

test('every owner page meets WCAG 2.1 AA automated checks', async ({
  page,
}, info) => {
  test.setTimeout(300000);
  await signIn(
    page.request,
    `a11y-${info.project.name}-${Date.now()}@test.example`,
    'Synthetic-Accessibility-2026!',
    'Accessibility audit',
  );
  const failures: { page: string; rule: string; nodes: number }[] = [];
  for (const path of pages) {
    await page.goto('/' + path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.waitForLoadState('networkidle');
    const result = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    for (const v of result.violations)
      failures.push({ page: path, rule: v.id, nodes: v.nodes.length });
  }
  expect(failures).toEqual([]);
});
