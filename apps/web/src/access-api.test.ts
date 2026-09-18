import { afterEach, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.unstubAllGlobals();
  vi.resetModules();
});
const token = (value: string) =>
  new Response(JSON.stringify({ token: value }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
const ok = () => new Response(null, { status: 204 });

it('reuses a request token for inventory commands and renews after login', async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce(token('anonymous'))
    .mockResolvedValueOnce(ok())
    .mockResolvedValueOnce(ok())
    .mockResolvedValueOnce(ok())
    .mockResolvedValueOnce(token('signed-in'))
    .mockResolvedValueOnce(ok());
  vi.stubGlobal('fetch', fetchMock);
  const { api } = await import('./access-api');
  await api('/api/workspace/inventory/units', 'POST', { code: 'KG' }, 'tenant');
  await api(
    '/api/workspace/inventory/categories',
    'POST',
    { name: 'Parts' },
    'tenant',
  );
  expect(fetchMock).toHaveBeenCalledTimes(3);
  expect(fetchMock.mock.calls[2][1].headers['X-CSRF-TOKEN']).toBe('anonymous');
  await api('/api/auth/login', 'POST', {
    email: 'synthetic@test.example',
    password: 'synthetic',
  });
  await api('/api/workspace/inventory/units', 'POST', { code: 'EA' }, 'tenant');
  expect(fetchMock.mock.calls[4][0]).toBe('/api/auth/csrf');
  expect(fetchMock.mock.calls[5][1].headers['X-CSRF-TOKEN']).toBe('signed-in');
});

it('invalidates a rejected token without automatically replaying a command', async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValueOnce(token('stale'))
    .mockResolvedValueOnce(
      new Response(JSON.stringify({ title: 'Refresh the page' }), {
        status: 400,
      }),
    )
    .mockResolvedValueOnce(token('fresh'))
    .mockResolvedValueOnce(ok());
  vi.stubGlobal('fetch', fetchMock);
  const { api } = await import('./access-api');
  await expect(
    api(
      '/api/workspace/inventory/documents',
      'POST',
      { idempotencyKey: 'same-key' },
      'tenant',
    ),
  ).rejects.toThrow('Refresh the page');
  expect(fetchMock).toHaveBeenCalledTimes(2);
  await api(
    '/api/workspace/inventory/documents',
    'POST',
    { idempotencyKey: 'same-key' },
    'tenant',
  );
  expect(fetchMock.mock.calls[3][1].headers['X-CSRF-TOKEN']).toBe('fresh');
  expect(fetchMock.mock.calls[3][1].body).toBe(fetchMock.mock.calls[1][1].body);
});
