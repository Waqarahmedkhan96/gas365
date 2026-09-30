import { act, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { language, setLanguage, tr } from './i18n';
import { LanguageToggle } from './components/LanguageToggle';
import { ur } from './ur';

afterEach(() => setLanguage('en'));

it('keeps English by default and fills placeholders', () => {
  expect(language()).toBe('en');
  expect(tr('Save customer')).toBe('Save customer');
  expect(
    tr('Showing {first}–{last} of {total} records · Page {page} of {pages}', {
      first: 1,
      last: 20,
      total: 42,
      page: 1,
      pages: 3,
    }),
  ).toBe('Showing 1–20 of 42 records · Page 1 of 3');
});

it('switches to Urdu with a right-to-left document and falls back to English for unknown text', () => {
  render(<LanguageToggle />);
  act(() => screen.getByRole('button', { name: /اردو/ }).click());
  expect(language()).toBe('ur');
  expect(document.documentElement.dir).toBe('rtl');
  expect(document.documentElement.lang).toBe('ur');
  expect(tr('Save customer')).toBe(ur['Save customer']);
  expect(tr('A sentence nobody translated')).toBe(
    'A sentence nobody translated',
  );
  expect(screen.getByRole('button', { name: /English/ })).toBeInTheDocument();
});

it('keeps every placeholder in every Urdu entry', () => {
  for (const [english, urdu] of Object.entries(ur)) {
    const placeholders = (text: string) =>
      (text.match(/\{[a-z]+\}/gi) ?? []).sort().join();
    expect(placeholders(urdu), english).toBe(placeholders(english));
  }
});
