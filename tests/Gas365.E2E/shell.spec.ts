import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function mockWorkspace(page: Page, owner = true) {
  const tenantId = 'tenant-sample';
  const responses: Record<string, unknown> = {
    '/api/auth/me': {
      userId: 'user-sample',
      name: owner ? 'Ayesha Khan' : 'Team colleague',
      email: 'sample@test.example',
      tenantId,
      membershipId: 'membership-sample',
      permissions: owner
        ? [
            'tenant.manage',
            'branches.manage',
            'users.manage',
            'roles.manage',
            'audit.view',
          ]
        : [],
      isOwner: owner,
      workspaces: [
        { membershipId: 'membership-sample', tenantId, name: 'Karachi LPG' },
      ],
    },
    '/api/workspace/tenant': {
      tenantId,
      name: 'Karachi LPG',
      timeZone: 'Asia/Karachi',
      maxBranches: 3,
      maxUsers: 10,
      maxAdmins: 3,
    },
    '/api/workspace/branches': [
      { id: 'branch-main', name: 'Main branch', active: true, version: 1 },
      ...(owner
        ? [
            {
              id: 'branch-north',
              name: 'North branch',
              active: true,
              version: 1,
            },
          ]
        : []),
    ],
    '/api/workspace/roles': [],
    '/api/workspace/members': [],
    '/api/workspace/audit': [],
    '/api/auth/sessions': [],
  };
  await page.route('**/api/**', (route) => {
    const path = new URL(route.request().url()).pathname;
    return route.fulfill({
      status: path in responses ? 200 : 404,
      json: responses[path] || {},
    });
  });
}

async function checkAccessibility(page: Page) {
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(result.violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
}

test('shell supports branch selection, keyboard search and mobile navigation', async ({
  page,
}, testInfo) => {
  await mockWorkspace(page);
  await page.goto('/workspace');
  await expect(
    page.getByRole('heading', { name: 'Overview', exact: true }),
  ).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(
    page.getByRole('link', { name: 'Skip to main content' }),
  ).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#workspace-content')).toBeFocused();
  await page
    .getByRole('combobox', { name: 'Active branch', exact: true })
    .selectOption('branch-north');
  await page.reload();
  await expect(
    page.getByRole('combobox', { name: 'Active branch', exact: true }),
  ).toHaveValue('branch-north');
  await checkAccessibility(page);
  await page.screenshot({
    path: testInfo.outputPath('shell-overview.png'),
    fullPage: true,
  });
  await page.getByRole('button', { name: 'Search pages', exact: true }).focus();
  await page.keyboard.press('Control+k');
  await expect(
    page.getByRole('dialog', { name: 'Go to a page' }),
  ).toBeVisible();
  await expect(page.getByLabel('Find a page')).toBeFocused();
  await page.keyboard.type('Branches');
  await checkAccessibility(page);
  await page.keyboard.press('Tab');
  await expect(
    page.getByRole('dialog').getByRole('link', { name: 'Branches' }),
  ).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/workspace\/branches$/);
  await expect(page.locator('#workspace-content')).toBeFocused();
  const menu = page.getByRole('button', { name: 'Open navigation' });
  if (await menu.isVisible()) {
    await menu.click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await checkAccessibility(page);
    await page.keyboard.press('Escape');
    await expect(menu).toBeFocused();
  }
  await page.setViewportSize({ width: 320, height: 800 });
  await checkAccessibility(page);
});

test('worker gets a denied state for direct restricted routes and a valid branch', async ({
  page,
}) => {
  await mockWorkspace(page, false);
  await page.addInitScript(() =>
    sessionStorage.setItem('branch:tenant-sample', 'foreign-branch'),
  );
  await page.goto('/workspace/team');
  await expect(
    page.getByRole('heading', { name: 'Access restricted' }),
  ).toBeVisible();
  await expect(
    page.getByRole('combobox', { name: 'Active branch', exact: true }),
  ).toHaveValue('branch-main');
  await page.getByRole('button', { name: 'Search pages', exact: true }).click();
  await expect(
    page.getByRole('dialog').getByRole('link', { name: 'Team', exact: true }),
  ).toHaveCount(0);
  await page.keyboard.press('Escape');
  await checkAccessibility(page);
});

test('component showcase covers confirmation, form errors, pagination and accessible states', async ({
  page,
}, testInfo) => {
  const browserErrors: string[] = [];
  page.on('pageerror', (error) => browserErrors.push(error.message));
  page.on('console', (message) => {
    if (
      message.type() === 'error' &&
      /Content Security Policy/i.test(message.text())
    )
      browserErrors.push(message.text());
  });
  await mockWorkspace(page);
  await page.goto('/workspace/showcase');
  await expect(
    page.getByRole('heading', { name: 'Component showcase', exact: true }),
  ).toBeVisible();
  await checkAccessibility(page);
  await page.screenshot({
    path: testInfo.outputPath('component-showcase.png'),
    fullPage: true,
  });
  await page.getByRole('button', { name: 'Validate sample' }).click();
  await expect(
    page.getByRole('textbox', { name: /Sample name/ }),
  ).toHaveAttribute('aria-invalid', 'true');
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(page.getByText('Page 2 of 2 · 3 records')).toBeVisible();
  await page.getByRole('button', { name: 'Preview confirmation' }).click();
  await expect(
    page.getByRole('button', { name: 'Cancel', exact: true }),
  ).toBeFocused();
  await page.keyboard.press('Shift+Tab');
  await expect(
    page.getByRole('button', { name: 'Confirm', exact: true }),
  ).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(
    page.getByRole('button', { name: 'Cancel', exact: true }),
  ).toBeFocused();
  await checkAccessibility(page);
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  await page.getByRole('button', { name: 'Show notification' }).click();
  await expect(
    page.getByText('The sample action completed. No business data changed.', {
      exact: true,
    }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Dismiss notification' }).click();
  for (const state of ['loading', 'error', 'denied', 'empty']) {
    await page.getByLabel('Preview state').selectOption(state);
    await checkAccessibility(page);
  }
  expect(browserErrors).toEqual([]);
});

test('workspace recovers after a failed request', async ({ page }) => {
  await mockWorkspace(page);
  await page.route('**/api/workspace/tenant', (route) => route.abort());
  await page.goto('/workspace');
  await expect(
    page.getByRole('heading', { name: 'Workspace unavailable' }),
  ).toBeVisible();
  await page.unroute('**/api/workspace/tenant');
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect(
    page.getByRole('heading', { name: 'Overview', exact: true }),
  ).toBeVisible();
});
