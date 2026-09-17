import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { Field } from './ui/form';
import { ConfirmButton } from './ui/feedback';
import { ShowcasePage } from '../ShowcasePage';
import { canVisit, pages } from '../navigation';
import type { Me } from '../access-api';
import { readFileSync } from 'node:fs';

it('associates field errors with the input and preserves the required label', () => {
  render(<Field label="Sample name" required error="Enter a name." />);
  const input = screen.getByRole('textbox', { name: /Sample name/ });
  expect(input).toBeRequired();
  expect(input).toHaveAttribute('aria-invalid', 'true');
  expect(input).toHaveAccessibleDescription('Enter a name.');
});

it('focuses cancel in a confirmation and returns focus without executing the action', async () => {
  const user = userEvent.setup();
  const action = vi.fn();
  render(
    <ConfirmButton
      title="End session?"
      description="Sign in again to return."
      onConfirm={action}
    >
      End session
    </ConfirmButton>,
  );
  const trigger = screen.getByRole('button', { name: 'End session' });
  await user.click(trigger);
  expect(screen.getByRole('alertdialog')).toHaveAccessibleName('End session?');
  expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus();
  await user.keyboard('{Escape}');
  expect(action).not.toHaveBeenCalled();
  expect(trigger).toHaveFocus();
  await user.click(trigger);
  await user.click(screen.getByRole('button', { name: 'Confirm' }));
  expect(action).toHaveBeenCalledTimes(1);
});

it('paginates and filters sample records without exposing hidden pages', async () => {
  const user = userEvent.setup();
  render(<ShowcasePage />);
  const table = screen.getByRole('table', { name: 'Sample records' });
  expect(within(table).queryByText('SAMPLE-003')).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Next' }));
  expect(within(table).getByText('SAMPLE-003')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  fireEvent.change(
    screen.getByRole('textbox', { name: 'Filter sample records' }),
    { target: { value: 'missing' } },
  );
  expect(screen.getByText('No records found')).toBeInTheDocument();
});

it('uses one permission policy for route and navigation decisions', () => {
  const worker = { permissions: [], isOwner: false } as unknown as Me;
  expect(
    pages.filter((page) => canVisit(page, worker)).map((page) => page.id),
  ).toEqual(['overview', 'branches', 'sessions']);
  const admin = { ...worker, permissions: ['users.manage', 'audit.view'] };
  expect(
    canVisit(
      pages.find((page) => page.id === 'team')!,
      admin,
    ),
  ).toBe(true);
  expect(
    canVisit(
      pages.find((page) => page.id === 'showcase')!,
      admin,
    ),
  ).toBe(false);
});

it('uses AA text colors and distinguishable input boundaries', () => {
  const css = readFileSync('src/styles.css', 'utf8');
  function token(name: string): string {
    const value = css.match(new RegExp(`${name}:\\s*([^;]+);`))![1];
    return value.startsWith('var(')
      ? token(value.slice(4, -1))
      : value.replace('#', '');
  }
  function luminance(hex: string) {
    const values = hex.match(/[a-f\d]{2}/gi)!.map((value) => {
      const n = parseInt(value, 16) / 255;
      return n <= 0.04045 ? n / 12.92 : ((n + 0.055) / 1.055) ** 2.4;
    });
    return values[0] * 0.2126 + values[1] * 0.7152 + values[2] * 0.0722;
  }
  function ratio(a: string, b: string) {
    const x = luminance(a),
      y = luminance(b);
    return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
  }
  for (const [foreground, background] of [
    ['--primary', '--surface'],
    ['--muted-foreground', '--page'],
    ['--text', '--surface'],
    ['--surface', '--destructive'],
  ])
    expect(ratio(token(foreground), token(background))).toBeGreaterThanOrEqual(
      4.5,
    );
  expect(ratio(token('--input'), token('--surface'))).toBeGreaterThanOrEqual(3);
});
