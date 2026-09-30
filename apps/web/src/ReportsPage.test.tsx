import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import { ReportsPage } from './ReportsPage';
import { Dashboard } from './Dashboard';
import { BranchContext } from './components/branch-context';
import { api, type Me } from './access-api';

vi.mock('./access-api', () => ({ api: vi.fn() }));
const user: Me = {
  userId: 'user',
  tenantId: 'tenant',
  membershipId: 'member',
  name: 'Owner',
  email: 'owner@example.test',
  permissions: ['reports.view', 'reports.export'],
  isOwner: true,
  workspaces: [],
};
const wrap = (node: React.ReactNode) => (
  <QueryClientProvider
    client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
  >
    <MemoryRouter>
      <BranchContext.Provider value="branch">{node}</BranchContext.Provider>
    </MemoryRouter>
  </QueryClientProvider>
);

it('shows server report rows with totals and queues idempotent exports', async () => {
  const calls: { path: string; method?: string; body?: unknown }[] = [];
  vi.mocked(api).mockImplementation(async (path, method, body) => {
    calls.push({ path, method, body });
    if (path === '/api/workspace/reports')
      return [
        {
          key: 'expenses',
          title: 'Expenses',
          definition: 'Posted expenses net of reversals.',
          reconciliation: 'Total equals expense cash.',
          types: ['Operating', 'Capital'],
          dateRange: true,
          branchScoped: true,
        },
      ];
    if (path.startsWith('/api/workspace/reports/expenses'))
      return {
        key: 'expenses',
        title: 'Expenses',
        definition: '',
        scope: 'All accessible branches',
        columns: [
          { key: 'category', label: 'Category', type: 'text' },
          { key: 'amount', label: 'Amount', type: 'money' },
        ],
        rows: [['Electricity', 1234.5]],
        totals: [null, 1234.5],
        totalRows: 1,
        page: 1,
        pageSize: 100,
        generatedAt: '2026-09-29T10:00:00Z',
      };
    if (path.endsWith('/exports') && method === 'POST') return {};
    if (path.endsWith('/exports')) return { items: [] };
    return [];
  });
  render(
    wrap(
      <ReportsPage
        user={user}
        branches={[{ id: 'b1', name: 'Main' }]}
        timeZone="Asia/Karachi"
      />,
    ),
  );
  const table = await screen.findByRole('table');
  expect(within(table).getByText('Electricity')).toBeInTheDocument();
  expect(
    within(table).getByRole('rowheader', { name: 'Total' }),
  ).toBeInTheDocument();
  expect(within(table).getAllByText(/1,234\.50/)).toHaveLength(2);
  expect(
    screen.getByText(/Reconciliation: Total equals expense cash/),
  ).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Export XLSX' }));
  await waitFor(() =>
    expect(calls).toContainEqual(
      expect.objectContaining({
        path: '/api/workspace/reports/exports',
        method: 'POST',
        body: expect.objectContaining({
          reportKey: 'expenses',
          format: 'xlsx',
          branchId: null,
          idempotencyKey: expect.any(String),
        }),
      }),
    ),
  );
  expect(await screen.findByRole('status')).toHaveTextContent(
    'XLSX export queued',
  );
});

it('dashboard shows permitted server metrics with a table alternative for the chart', async () => {
  vi.mocked(api).mockResolvedValue({
    from: '2026-09-28',
    to: '2026-09-29',
    scope: 'All accessible branches',
    metrics: [
      { key: 'revenue', label: 'Net revenue', value: 5000, type: 'money' },
    ],
    trend: [
      { date: '2026-09-28', revenue: 2000, grossProfit: null, expenses: null },
      { date: '2026-09-29', revenue: 3000, grossProfit: null, expenses: null },
    ],
    expensesByCategory: [],
    revenueByBranch: [],
    profitVisible: false,
    financeVisible: false,
  });
  render(wrap(<Dashboard user={user} timeZone="Asia/Karachi" />));
  const figures = await screen.findByLabelText('Key figures');
  expect(within(figures).getByText('Net revenue')).toBeInTheDocument();
  expect(within(figures).getByText(/5,000.00/)).toBeInTheDocument();
  const table = screen.getByRole('table', { name: 'Daily trend' });
  expect(
    within(table)
      .getAllByRole('columnheader')
      .map((h) => h.textContent),
  ).toEqual(['Date', 'Net revenue']);
  expect(within(table).getByText(/3,000\.00/)).toBeInTheDocument();
});
