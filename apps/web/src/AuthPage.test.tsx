import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, expect, it, vi } from 'vitest';
import { AuthPage } from './AuthPage';

afterEach(() => {
  vi.unstubAllGlobals();
  window.history.replaceState(null, '', '/');
});

// The sign-in page asks which external providers are enabled; answer that by URL so queued mocks stay in order.
function withProviders(
  mock: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
) {
  return vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
    String(input).endsWith('/api/auth/providers')
      ? Promise.resolve(new Response('{"google":false}'))
      : mock(input, init),
  );
}

function renderPage(mode: 'login' | 'confirm') {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <Routes>
          <Route path="/" element={<AuthPage mode={mode} />} />
          <Route path="/workspace" element={<h1>Signed in workspace</h1>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

it('recovers from rejected credentials and navigates after a successful retry', async () => {
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(new Response('{"token":"csrf-one"}'))
    .mockResolvedValueOnce(
      new Response('{"title":"Unable to sign in."}', { status: 401 }),
    )
    .mockResolvedValueOnce(new Response('{"token":"csrf-two"}'))
    .mockResolvedValueOnce(new Response(null, { status: 204 }));
  vi.stubGlobal('fetch', withProviders(fetch));
  renderPage('login');
  fireEvent.change(screen.getByLabelText('Email address'), {
    target: { value: 'synthetic@test.example' },
  });
  fireEvent.change(screen.getByLabelText('Password'), {
    target: { value: 'Synthetic-Password-2026!' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Unable to sign in.',
  );
  expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Signed in workspace')).toBeInTheDocument();
  expect(fetch.mock.calls[3][1].headers['X-CSRF-TOKEN']).toBe('csrf-two');
});

it('removes verification tokens from browser history and keeps them only for submission', async () => {
  window.history.replaceState(
    null,
    '',
    '/?email=synthetic%40test.example&token=synthetic-link-token',
  );
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(new Response('{"token":"csrf"}'))
    .mockResolvedValueOnce(new Response(null, { status: 204 }));
  vi.stubGlobal('fetch', fetch);
  renderPage('confirm');
  expect(window.location.search).toBe('');
  fireEvent.click(screen.getByRole('button', { name: 'Verify email' }));
  expect(await screen.findByRole('status')).toHaveTextContent('Email verified');
  expect(JSON.parse(fetch.mock.calls[1][1].body).token).toBe(
    'synthetic-link-token',
  );
});
