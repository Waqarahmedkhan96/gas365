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
import { OnboardingChecklist, SubscriptionPage } from './SubscriptionPage';
import { PlatformPage } from './PlatformPage';

vi.mock('./access-api', () => ({ api: vi.fn() }));
const owner: Me = {
  userId: 'user',
  tenantId: 'tenant',
  membershipId: 'member',
  name: 'Owner',
  email: 'owner@example.test',
  permissions: ['tenant.manage'],
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
const plan = (code: string, name: string, branches: number) => ({
  id: code,
  code,
  name,
  monthlyPrice: code === 'starter' ? 2500 : null,
  maxBranches: branches,
  maxUsers: 10,
  maxAdmins: 3,
  active: true,
  version: 1,
});

it('shows trial status, usage against limits and requests a plan', async () => {
  const calls: { path: string; method?: string; body?: unknown }[] = [];
  vi.mocked(api).mockImplementation(async (path, method, body) => {
    calls.push({ path, method, body });
    if (path.endsWith('/subscription'))
      return {
        plan: plan('business', 'Business', 3),
        status: 'Trial',
        trialEndsAt: new Date(Date.now() + 5 * 86_400_000).toISOString(),
        paidThrough: null,
        requestedPlan: null,
        usage: {
          month: '2026-09',
          branches: 1,
          users: 2,
          admins: 1,
          salesInvoices: 12,
          purchaseInvoices: 3,
          financeEntries: 7,
          stockDocuments: 4,
          exports: 0,
          importedRows: 0,
          storageBytes: 0,
        },
        plans: [plan('starter', 'Starter', 1), plan('business', 'Business', 3)],
        history: [],
        paymentProvider: 'Manual',
      };
    if (path.endsWith('/subscription/request'))
      return { provider: 'Manual', instructions: 'Pay by bank transfer.' };
    return [];
  });
  render(wrap(<SubscriptionPage user={owner} timeZone="Asia/Karachi" />));
  expect(await screen.findByText(/5 days left/)).toBeInTheDocument();
  const usage = screen.getByRole('table', {
    name: /Usage against your plan limits/,
  });
  const branches = within(usage).getByText('Active branches').closest('tr')!;
  expect(within(branches).getByText('1')).toBeInTheDocument();
  expect(within(branches).getByText('3')).toBeInTheDocument();
  expect(screen.getByText(/2,500\.00 per month/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Choose Starter' }));
  await waitFor(() =>
    expect(calls).toContainEqual({
      path: '/api/workspace/subscription/request',
      method: 'POST',
      body: { planId: 'starter' },
    }),
  );
  expect(await screen.findByText('Pay by bank transfer.')).toBeInTheDocument();
});

it('lists onboarding steps with links until they are done', async () => {
  vi.mocked(api).mockResolvedValue({
    dismissed: false,
    steps: [
      {
        key: 'warehouse',
        label: 'Add a warehouse',
        page: 'inventory',
        done: true,
      },
      {
        key: 'customers',
        label: 'Add customers',
        page: 'customers',
        done: false,
      },
    ],
  });
  render(wrap(<OnboardingChecklist user={owner} />));
  expect(
    await screen.findByText('1 of 2 steps done.', { exact: false }),
  ).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Add customers' })).toHaveAttribute(
    'href',
    '/workspace/customers',
  );
  expect(
    screen.queryByRole('link', { name: 'Add a warehouse' }),
  ).not.toBeInTheDocument();
});

it('platform admins record payments without seeing business finances', async () => {
  const calls: { path: string; method?: string; body?: unknown }[] = [];
  vi.mocked(api).mockImplementation(async (path, method, body) => {
    calls.push({ path, method, body });
    if (path.startsWith('/api/platform/tenants?'))
      return {
        items: [
          {
            tenantId: 't1',
            name: 'Ali Gas',
            plan: 'Business',
            status: 'Trial',
            trialEndsAt: '2026-10-10T00:00:00Z',
            paidThrough: null,
            requestedPlan: 'Starter',
            users: 2,
            branches: 1,
            transactionsThisMonth: 40,
            createdAt: '2026-09-20T00:00:00Z',
          },
        ],
        total: 1,
      };
    if (path === '/api/platform/plans')
      return [plan('starter', 'Starter', 1), plan('business', 'Business', 3)];
    return {};
  });
  render(wrap(<PlatformPage timeZone="Asia/Karachi" />));
  expect(await screen.findByText(/requested Starter/)).toBeInTheDocument();
  expect(screen.getByText(/40 transactions this month/)).toBeInTheDocument();
  // Only counts are shown; no business money figures.
  expect(screen.queryByText(/PKRs*[d,]+.d{2}/)).not.toBeInTheDocument();
  fireEvent.click(screen.getByText('Record payment and activate'));
  fireEvent.change(screen.getByLabelText('Amount received (PKR)'), {
    target: { value: '2500' },
  });
  fireEvent.change(screen.getByLabelText('Payment reference'), {
    target: { value: 'HBL-1' },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Activate plan' }));
  await waitFor(() =>
    expect(calls).toContainEqual({
      path: '/api/platform/tenants/t1/activate',
      method: 'POST',
      body: { planId: 'starter', months: 1, amount: 2500, reference: 'HBL-1' },
    }),
  );
});
