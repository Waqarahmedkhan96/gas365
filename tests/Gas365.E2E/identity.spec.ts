import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('owner verifies email, signs in, manages a branch and signs out', async ({
  page,
  request,
}, testInfo) => {
  test.setTimeout(60000);
  const email = `browser-${testInfo.project.name}-${Date.now()}@test.example`;
  const password = 'Synthetic-Browser-2026!';
  await page.goto('/register');
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
  await page.screenshot({
    path: testInfo.outputPath('registration.png'),
    fullPage: true,
  });
  await page.getByLabel('Your name', { exact: true }).fill('Browser owner');
  await page
    .getByLabel('Business name', { exact: true })
    .fill('Browser workspace');
  await page.getByLabel('Email address', { exact: true }).fill(email);
  await page.getByLabel(/^Password/).fill(password);
  await page
    .getByRole('button', { name: 'Create workspace', exact: true })
    .click();
  await expect(page.getByRole('status')).toContainText('Check your email', {
    timeout: 20000,
  });
  let link = '';
  await expect
    .poll(async () => {
      const response = await request.get(
        'http://127.0.0.1:8025/api/v1/messages',
      );
      const inbox = await response.json();
      const message = inbox.messages.find(
        (item: { To: { Address: string }[] }) =>
          item.To.some((recipient) => recipient.Address === email),
      );
      if (!message) return false;
      const details = await (
        await request.get(`http://127.0.0.1:8025/api/v1/message/${message.ID}`)
      ).json();
      link = details.Text.match(/http[^\s]+\/confirm[^\s]+/)?.[0] || '';
      return !!link;
    })
    .toBe(true);
  const url = new URL(link);
  await page.goto(url.pathname + url.search);
  await page.getByRole('button', { name: 'Verify email', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Email verified');
  await page.goto('/login');
  await page.getByLabel('Email address', { exact: true }).fill(email);
  await page.getByLabel(/^Password/).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Overview', exact: true }),
  ).toBeVisible();
  if (await page.getByRole('button', { name: 'Open navigation' }).isVisible())
    await page.getByRole('button', { name: 'Open navigation' }).click();
  await page.getByRole('link', { name: 'Branches', exact: true }).click();
  await page
    .getByLabel('New branch name', { exact: true })
    .fill('Second location');
  await page.getByRole('button', { name: 'Add branch', exact: true }).click();
  await expect(
    page.getByLabel('Branch name', { exact: true }).nth(1),
  ).toHaveValue('Second location');
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
  await page.screenshot({
    path: testInfo.outputPath('workspace.png'),
    fullPage: true,
  });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.getByRole('button', { name: /account menu$/ }).click();
  await page.getByRole('menuitem', { name: 'Sign out', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Welcome back' }),
  ).toBeVisible();
  expect((await page.request.get('/api/auth/me')).status()).toBe(401);
});
