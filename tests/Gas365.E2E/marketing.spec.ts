import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('the public website explains Gas365, shows live plans and leads to sign-up', async ({
  page,
}) => {
  await page.goto('/');
  await expect(
    page.getByRole('heading', {
      level: 1,
      name: 'Run your LPG business from one clear screen',
    }),
  ).toBeVisible();
  const pricing = page.locator('#pricing');
  await expect(pricing.getByRole('listitem').first()).toBeVisible();
  await pricing.getByRole('radio', { name: /Yearly/ }).click();
  await expect(pricing.getByRole('radio', { name: /Yearly/ })).toHaveAttribute(
    'aria-checked',
    'true',
  );
  await page.getByText('How does the free trial work?').click();
  await expect(page.getByText(/14 days of full access/)).toBeVisible();
  const accessibility = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa'])
    .analyze();
  expect(accessibility.violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  const menu = page.getByRole('button', { name: 'Open menu' });
  if (await menu.isVisible()) {
    await menu.click();
    await expect(
      page.getByRole('navigation', { name: 'Website' }),
    ).toBeVisible();
  }
  await page
    .getByRole('navigation', { name: 'Website' })
    .getByRole('link', { name: 'Start free trial' })
    .click();
  await expect(page).toHaveURL(/\/register$/);
  await expect(
    page.getByRole('heading', { name: 'Create your workspace' }),
  ).toBeVisible();
});
