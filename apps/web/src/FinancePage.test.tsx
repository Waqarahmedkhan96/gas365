import { render, screen } from '@testing-library/react';
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
  expect(await screen.findByText(/Drawer.*123\.45/)).toBeInTheDocument();
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
