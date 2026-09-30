import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { expect, it, vi } from 'vitest';
import { FinancePage } from './FinancePage';
import { BranchContext } from './components/branch-context';
import { api, type Me } from './access-api';

vi.mock('./access-api', () => ({ api: vi.fn() }));
it('read-only finance access shows server balances without posting or account controls', async () => {
  vi.mocked(api).mockImplementation(async (path) => {
    if (path.includes('/accounts'))
      return [
        {
          id: 'cash',
          name: 'Drawer',
          kind: 'Cash',
          defaultCash: true,
          balance: 123.45,
        },
      ];
    if (path.includes('/entries'))
      return { items: [], total: 0, page: 1, pageSize: 20 };
    return [];
  });
  const user: Me = {
    userId: 'user',
    tenantId: 'tenant',
    membershipId: 'member',
    name: 'Reader',
    email: 'reader@example.test',
    permissions: ['finance.view'],
    isOwner: false,
    workspaces: [],
  };
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <BranchContext.Provider value="branch">
        <FinancePage user={user} section="expenses" timeZone="Asia/Karachi" />
      </BranchContext.Provider>
    </QueryClientProvider>,
  );
  expect(
    await screen.findByLabelText('Cash and bank balances'),
  ).toHaveTextContent(/Drawer.*123\.45/);
  expect(
    screen.queryByRole('button', { name: 'Post transaction' }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByText('Create account', { exact: true }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Add category' }),
  ).not.toBeInTheDocument();
});

it('finance managers rename or retire accounts with the row version and cannot post to retired accounts', async () => {
  const calls: { path: string; method?: string; body?: unknown }[] = [];
  vi.mocked(api).mockImplementation(async (path, method, body) => {
    calls.push({ path, method, body });
    if (path.includes('/accounts'))
      return [
        {
          id: 'drawer',
          name: 'Drawer',
          kind: 'Cash',
          defaultCash: true,
          balance: 0,
          active: true,
          version: 7,
        },
        {
          id: 'old-bank',
          name: 'Old bank',
          kind: 'Bank',
          defaultCash: false,
          balance: 0,
          active: false,
          version: 3,
        },
      ];
    if (path.includes('/entries'))
      return { items: [], total: 0, page: 1, pageSize: 20 };
    return [];
  });
  const user: Me = {
    userId: 'user',
    tenantId: 'tenant',
    membershipId: 'member',
    name: 'Manager',
    email: 'manager@example.test',
    permissions: ['finance.view', 'finance.post', 'finance.manage'],
    isOwner: false,
    workspaces: [],
  };
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <BranchContext.Provider value="branch">
        <FinancePage user={user} section="loans" timeZone="Asia/Karachi" />
      </BranchContext.Provider>
    </QueryClientProvider>,
  );
  expect(
    await screen.findByLabelText('Cash and bank balances'),
  ).toHaveTextContent(/Old bank.*retired/);
  const payment = screen.getByLabelText('Payment account');
  expect(
    within(payment).queryByRole('option', { name: 'Old bank' }),
  ).not.toBeInTheDocument();
  expect(
    within(payment).getByRole('option', { name: 'Drawer' }),
  ).toBeInTheDocument();
  const name = screen.getByLabelText(/Name for Drawer/);
  fireEvent.change(name, { target: { value: 'Front counter' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save Drawer' }));
  await waitFor(() =>
    expect(calls).toContainEqual({
      path: '/api/workspace/finance/accounts/drawer',
      method: 'PUT',
      body: { name: 'Front counter', active: true, version: 7 },
    }),
  );
});
