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
import { api, type Me } from './access-api';
import { NotificationBell, NotificationsPage } from './NotificationsPage';
import { AuditPage } from './AuditPage';
import { ImportsPage } from './ImportsPage';

vi.mock('./access-api', () => ({ api: vi.fn() }));
const user: Me = {
  userId: 'user',
  tenantId: 'tenant',
  membershipId: 'member',
  name: 'Owner',
  email: 'owner@example.test',
  permissions: ['audit.view', 'imports.manage'],
  isOwner: true,
  workspaces: [],
};
const wrap = (node: React.ReactNode) => (
  <QueryClientProvider
    client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
  >
    <MemoryRouter>{node}</MemoryRouter>
  </QueryClientProvider>
);

it('announces unread alerts and marks one as read', async () => {
  const calls: { path: string; method?: string }[] = [];
  vi.mocked(api).mockImplementation(async (path, method) => {
    calls.push({ path, method });
    if (path.endsWith('/summary')) return { unread: 2 };
    if (method === 'POST') return undefined;
    return {
      items: [
        {
          id: 'n1',
          kind: 'OverdueCredit',
          title: 'Overdue credit: Hotel',
          body: 'PKR 900.00 unpaid for more than 30 days.',
          branchId: 'b',
          createdAt: '2026-09-29T10:00:00Z',
          read: false,
        },
      ],
      total: 1,
      pageSize: 20,
    };
  });
  render(
    wrap(
      <>
        <NotificationBell user={user} />
        <NotificationsPage user={user} timeZone="Asia/Karachi" />
      </>,
    ),
  );
  expect(
    await screen.findByRole('link', { name: 'Notifications, 2 unread' }),
  ).toBeInTheDocument();
  expect(
    await screen.findByText(/New · Overdue credit: Hotel/),
  ).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Mark as read' }));
  await waitFor(() =>
    expect(calls).toContainEqual({
      path: '/api/workspace/notifications/n1/read',
      method: 'POST',
    }),
  );
});

it('lists corrections with reason, author and amount', async () => {
  vi.mocked(api).mockResolvedValue({
    items: [
      {
        module: 'finance',
        kind: 'ExpenseReversal',
        originalId: 'aaaaaaaa-1111',
        reversalId: 'r1',
        branch: 'Main',
        businessDate: '2026-09-29',
        amount: 5,
        reason: 'Duplicate',
        actor: 'Owner',
        postedAt: '2026-09-29T10:00:00Z',
      },
    ],
    total: 1,
    pageSize: 20,
  });
  render(wrap(<AuditPage user={user} branches={[]} timeZone="Asia/Karachi" />));
  fireEvent.click(
    screen.getByRole('button', { name: 'Corrections and reversals' }),
  );
  const table = await screen.findByRole('table');
  expect(within(table).getByText('Duplicate')).toBeInTheDocument();
  expect(within(table).getByText(/5\.00/)).toBeInTheDocument();
  expect(within(table).getByText('aaaaaaaa')).toBeInTheDocument();
});

it('shows import columns and the preview with row problems', async () => {
  vi.mocked(api).mockImplementation(async (path) => {
    if (path.endsWith('/types'))
      return [
        {
          key: 'customers',
          title: 'Customers',
          description: 'Customer contacts.',
          fields: [
            {
              key: 'name',
              label: 'Name',
              required: true,
              hint: 'Up to 120 characters.',
            },
          ],
        },
      ];
    if (path === '/api/workspace/imports/b1')
      return {
        id: 'b1',
        type: 'customers',
        fileName: 'customers.csv',
        status: 'Invalid',
        headers: ['Client'],
        mapping: { name: null },
        rowCount: 1,
        errorCount: 1,
        committedCount: 0,
        createdAt: '2026-09-29T10:00:00Z',
        committedAt: null,
        version: 3,
        preview: [
          {
            rowNumber: 2,
            status: 'Invalid',
            values: { name: '' },
            errors: ['Name: Required.'],
          },
        ],
      };
    return {
      items: [
        {
          id: 'b1',
          type: 'customers',
          fileName: 'customers.csv',
          status: 'Invalid',
          rowCount: 1,
          committedCount: 0,
        },
      ],
    };
  });
  render(wrap(<ImportsPage user={user} />));
  expect(await screen.findByText('Up to 120 characters.')).toBeInTheDocument();
  fireEvent.click(await screen.findByRole('button', { name: 'customers.csv' }));
  const preview = await screen.findByRole('table', { name: /Preview/ });
  expect(within(preview).getByText('Name: Required.')).toBeInTheDocument();
  expect(screen.getByText(/1 rows need fixes/)).toBeInTheDocument();
  expect(
    screen.getByRole('button', { name: 'Download error list' }),
  ).toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: /Import 1 rows/ }),
  ).not.toBeInTheDocument();
});
