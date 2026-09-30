import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('visitors switch the website and sign-in pages to Urdu with a right-to-left layout', async ({
  page,
}) => {
  await page.goto('/');
  const toggle = page
    .getByRole('button', { name: 'اردو' })
    .filter({ visible: true });
  if (!(await toggle.count()))
    await page.getByRole('button', { name: 'Open menu' }).click();
  await page
    .getByRole('button', { name: 'اردو' })
    .filter({ visible: true })
    .first()
    .click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('html')).toHaveAttribute('lang', 'ur');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(
    'اپنا ایل پی جی کاروبار ایک صاف اسکرین سے چلائیں',
  );
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);

  // The choice is remembered on this device.
  await page.goto('/login');
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('خوش آمدید');
  await expect(
    page.getByRole('button', { name: 'سائن اِن کریں' }),
  ).toBeVisible();
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
  await expect(
    page.getByRole('heading', { level: 1, name: 'Welcome back' }),
  ).toBeVisible();
});
