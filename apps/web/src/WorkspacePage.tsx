import { lazy, Suspense, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, Navigate, useParams } from 'react-router-dom';
import { ApplicationShell } from './components/ApplicationShell';
import {
  KeyValues,
  PageHeading,
  PageState,
  StatusChip,
} from './components/page';
import { PageIcon } from './components/icons';
import { PageActionsSlot } from './components/page-actions';
import { Button } from './components/ui/button';
import { Input } from './components/ui/form';
import { ConfirmButton, Notification } from './components/ui/feedback';
import { canVisit, pages } from './navigation';
import { daysLeft } from './platform-types';
import {
  api,
  ApiError,
  type Me,
  type Tenant,
  type Branch,
  type Role,
  type Member,
  type Session,
} from './access-api';
import { tr } from './i18n';

const FinancePage = lazy(() =>
  import('./FinancePage').then((module) => ({ default: module.FinancePage })),
);
const ReportsPage = lazy(() =>
  import('./ReportsPage').then((module) => ({ default: module.ReportsPage })),
);
const ImportsPage = lazy(() =>
  import('./ImportsPage').then((module) => ({ default: module.ImportsPage })),
);
const AuditPage = lazy(() =>
  import('./AuditPage').then((module) => ({ default: module.AuditPage })),
);
const NotificationsPage = lazy(() =>
  import('./NotificationsPage').then((module) => ({
    default: module.NotificationsPage,
  })),
);
const SubscriptionPage = lazy(() =>
  import('./SubscriptionPage').then((module) => ({
    default: module.SubscriptionPage,
  })),
);
const OnboardingChecklist = lazy(() =>
  import('./SubscriptionPage').then((module) => ({
    default: module.OnboardingChecklist,
  })),
);
const SetupWizard = lazy(() =>
  import('./SetupWizard').then((module) => ({ default: module.SetupWizard })),
);
const PlatformPage = lazy(() =>
  import('./PlatformPage').then((module) => ({ default: module.PlatformPage })),
);
const Dashboard = lazy(() =>
  import('./Dashboard').then((module) => ({ default: module.Dashboard })),
);
const SalesDraftPage = lazy(() =>
  import('./SalesDraftPage').then((module) => ({
    default: module.SalesDraftPage,
  })),
);
const CustomersPage = lazy(() =>
  import('./CustomersPage').then((module) => ({
    default: module.CustomersPage,
  })),
);
const InventoryPage = lazy(() =>
  import('./InventoryPage').then((module) => ({
    default: module.InventoryPage,
  })),
);
const PurchasingPage = lazy(() =>
  import('./PurchasingPage').then((module) => ({
    default: module.PurchasingPage,
  })),
);
const ShowcasePage = lazy(() =>
  import('./ShowcasePage').then((module) => ({ default: module.ShowcasePage })),
);

const permissions = [
  'finance.view',
  'finance.post',
  'finance.manage',
  'finance.reverse',
  'register.close',
  'register.reopen',
  'sales.view',
  'sales.create',
  'sales.discount',
  'customers.view',
  'customers.manage',
  'prices.manage',
  'purchases.view_cost',
  'purchases.create',
  'purchases.post',
  'purchases.pay',
  'purchases.reverse',
  'suppliers.manage',
  'tenant.manage',
  'branches.manage',
  'users.manage',
  'roles.manage',
  'audit.view',
  'inventory.view',
  'catalog.manage',
  'inventory.post',
  'inventory.reverse',
];
const permissionLabels: Record<string, string> = {
  'finance.view': 'View financial ledgers and accounts',
  'finance.post': 'Record payments, expenses, loans and transfers',
  'finance.manage': 'Configure accounts, categories and cash adjustments',
  'finance.reverse': 'Reverse financial entries',
  'register.close': 'Approve cash-register closing',
  'register.reopen': 'Reopen closed cash registers',
  'sales.view': 'View sales drafts',
  'sales.create': 'Create and edit sales drafts',
  'sales.discount': 'Apply sales discounts',
  'customers.view': 'View customers and selling prices',
  'customers.manage': 'Manage customers',
  'prices.manage': 'Manage selling prices',
  'purchases.view_cost': 'View purchases and supplier costs',
  'purchases.create': 'Create and edit purchase drafts',
  'purchases.post': 'Post purchases',
  'purchases.pay': 'Record supplier cash payments',
  'purchases.reverse': 'Reverse purchases and payments',
  'suppliers.manage': 'Manage suppliers',
  'inventory.view': 'View inventory',
  'catalog.manage': 'Manage catalog and warehouses',
  'inventory.post': 'Post stock movements',
  'inventory.reverse': 'Reverse stock movements',
  'tenant.manage': 'Manage business settings',
  'branches.manage': 'Manage all branches',
  'users.manage': 'Manage team members',
  'roles.manage': 'Manage role definitions (owner only)',
  'audit.view': 'View activity history',
};
const data = (event: FormEvent<HTMLFormElement>) => {
  event.preventDefault();
  return new FormData(event.currentTarget);
};

export function WorkspacePage() {
  const client = useQueryClient();
  const me = useQuery({
    queryKey: ['me'],
    queryFn: () => api<Me>('/api/auth/me'),
    retry: false,
  });
  const { section = 'overview' } = useParams();
  const tab = section;
  const currentPage = pages.find((page) => page.id === tab);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [noticeId, setNoticeId] = useState(0);
  const [actionsSlot, setActionsSlot] = useState<HTMLDivElement | null>(null);
  const user = me.data;
  const has = (permission: string) => user?.permissions.includes(permission);
  const info = useQuery({
    queryKey: ['workspace', user?.tenantId, user?.permissions.join(',')],
    enabled: !!user,
    retry: false,
    queryFn: async () => {
      const get = <T,>(path: string) =>
        api<T>(`/api/workspace/${path}`, 'GET', undefined, user!.tenantId);
      const [tenant, branches, roles, members, sessions] = await Promise.all([
        get<Tenant>('tenant'),
        get<Branch[]>('branches'),
        has('users.manage') ? get<Role[]>('roles') : [],
        has('users.manage') ? get<Member[]>('members') : [],
        api<Session[]>('/api/auth/sessions'),
      ]);
      return { tenant, branches, roles, members, sessions };
    },
  });

  async function run(
    action: () => Promise<unknown>,
    success = 'Changes saved.',
  ) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
      await client.invalidateQueries({ queryKey: ['workspace'] });
      await client.invalidateQueries({ queryKey: ['me'] });
      setNotice(success);
      setNoticeId((value) => value + 1);
    } catch (failure) {
      if (
        failure instanceof ApiError &&
        (failure.status === 401 || failure.status === 403)
      )
        await client.invalidateQueries({ queryKey: ['me'] });
      setError(
        failure instanceof Error ? failure.message : 'Please try again.',
      );
    } finally {
      setBusy(false);
    }
  }
  const mutate = (path: string, method: string, body?: unknown) =>
    api(`/api/workspace/${path}`, method, body, user!.tenantId);

  if (me.error instanceof ApiError && me.error.status === 401)
    return <Navigate to="/login" replace />;
  if (!user || !info.data || me.isError || info.isError)
    return (
      <main className="initial-state">
        <h1>{tr('Your workspace')}</h1>
        <PageState
          kind={me.isError || info.isError ? 'error' : 'loading'}
          title={
            me.isError || info.isError
              ? tr('Workspace unavailable')
              : tr('Loading your workspace')
          }
          description={
            me.error?.message ||
            info.error?.message ||
            tr('Retrieving your workspace and permissions.')
          }
          retry={
            me.isError || info.isError
              ? () => {
                  void me.refetch();
                  if (user) void info.refetch();
                }
              : undefined
          }
        />
        <Link to="/login">{tr('Sign in')}</Link>
      </main>
    );
  const { tenant, branches, roles, members, sessions } = info.data;

  const allowed = currentPage && canVisit(currentPage, user);
  return (
    <ApplicationShell
      key={user.tenantId}
      user={user}
      tenant={tenant}
      branches={branches}
      busy={busy}
      onSignOut={() =>
        void run(async () => {
          await api('/api/auth/logout', 'POST');
          client.clear();
          window.location.assign('/login');
        })
      }
      onSwitch={(membershipId) =>
        void run(async () => {
          await api('/api/auth/switch', 'POST', { membershipId });
          client.clear();
          window.location.assign('/workspace');
        })
      }
    >
      {user.support && (
        <div className="support-banner">
          <strong>
            {tr('Support view of')} {user.support.tenantName}
          </strong>{' '}
          {tr('— read-only until')}{' '}
          {new Date(user.support.expiresAt).toLocaleString('en-PK', {
            timeZone: tenant.timeZone,
          })}
          {tr('. Every page you open is recorded for the owner.')}{' '}
          <Button
            variant="secondary"
            onClick={() =>
              void run(async () => {
                await api('/api/platform/support/exit', 'POST');
                client.clear();
                window.location.assign('/workspace');
              })
            }
          >
            {tr('Exit support view')}
          </Button>
        </div>
      )}
      {!user.support && tenant.subscriptionStatus === 'Expired' && (
        <div className="subscription-banner">
          <strong>{tr('Read-only:')}</strong>{' '}
          {tr(
            'your trial or plan has ended. Your data is safe and can still be viewed and exported.',
          )}{' '}
          {has('tenant.manage') ? (
            <Link to="/workspace/subscription">{tr('Choose a plan')}</Link>
          ) : (
            tr('Ask the business owner to choose a plan.')
          )}
        </div>
      )}
      {!user.support &&
        tenant.subscriptionStatus === 'Trial' &&
        tenant.trialEndsAt &&
        has('tenant.manage') && (
          <div className="subscription-banner">
            {tr('Free trial: {days} days left.', {
              days: daysLeft(tenant.trialEndsAt),
            })}{' '}
            <Link to="/workspace/subscription">{tr('Choose a plan')}</Link>
          </div>
        )}
      <PageHeading
        title={currentPage?.label || tr('Page not found')}
        description={
          currentPage?.description || tr('This page is not available.')
        }
        icon={currentPage?.icon}
        actionsRef={setActionsSlot}
      />
      {!currentPage ? (
        <PageState
          kind="empty"
          title={tr('Page not found')}
          description={tr('Choose a page from the workspace navigation.')}
        />
      ) : !allowed ? (
        <PageState
          kind="denied"
          title={tr('Access restricted')}
          description={tr(
            'Your role does not include access to this page. Ask your workspace owner if you need it.',
          )}
        />
      ) : (
        <PageActionsSlot value={actionsSlot}>
          {['udhaar', 'expenses', 'loans', 'cash-register'].includes(tab) && (
            <Suspense fallback={<p>{tr('Loading finances…')}</p>}>
              <FinancePage
                user={user}
                section={tab}
                timeZone={tenant.timeZone}
              />
            </Suspense>
          )}
          {tab === 'reports' && (
            <Suspense fallback={<p role="status">{tr('Loading reports…')}</p>}>
              <ReportsPage
                user={user}
                branches={branches}
                timeZone={tenant.timeZone}
              />
            </Suspense>
          )}
          {tab === 'notifications' && (
            <Suspense
              fallback={<p role="status">{tr('Loading notifications…')}</p>}
            >
              <NotificationsPage user={user} timeZone={tenant.timeZone} />
            </Suspense>
          )}
          {tab === 'imports' && (
            <Suspense fallback={<p role="status">{tr('Loading imports…')}</p>}>
              <ImportsPage user={user} />
            </Suspense>
          )}
          {tab === 'subscription' && (
            <Suspense
              fallback={<p role="status">{tr('Loading subscription…')}</p>}
            >
              <SubscriptionPage user={user} timeZone={tenant.timeZone} />
            </Suspense>
          )}
          {tab === 'platform' && (
            <Suspense fallback={<p role="status">{tr('Loading platform…')}</p>}>
              <PlatformPage timeZone={tenant.timeZone} />
            </Suspense>
          )}
          {tab === 'setup' && (
            <Suspense fallback={<p role="status">{tr('Loading setup…')}</p>}>
              <SetupWizard user={user} tenant={tenant} branches={branches} />
            </Suspense>
          )}
          {tab === 'overview' && !user.support && (
            <Suspense fallback={null}>
              <OnboardingChecklist user={user} />
            </Suspense>
          )}
          {tab === 'overview' && has('reports.view') && (
            <Suspense
              fallback={<p role="status">{tr('Loading dashboard…')}</p>}
            >
              <Dashboard user={user} timeZone={tenant.timeZone} />
            </Suspense>
          )}
          {tab === 'sales' && (
            <Suspense
              fallback={<p role="status">{tr('Loading sales drafts...')}</p>}
            >
              <SalesDraftPage user={user} timeZone={tenant.timeZone} />
            </Suspense>
          )}
          {(tab === 'customers' || tab === 'prices') && (
            <Suspense
              fallback={
                <p role="status">{tr('Loading customers and prices...')}</p>
              }
            >
              <CustomersPage
                key={`${user.tenantId}:${tab}`}
                user={user}
                pricesOnly={tab === 'prices'}
                timeZone={tenant.timeZone}
              />
            </Suspense>
          )}
          {(tab === 'purchases' || tab === 'suppliers') && (
            <Suspense
              fallback={<p role="status">{tr('Loading purchasing…')}</p>}
            >
              <PurchasingPage
                user={user}
                suppliersOnly={tab === 'suppliers'}
                timeZone={tenant.timeZone}
              />
            </Suspense>
          )}
          {tab === 'inventory' && (
            <Suspense
              fallback={<p role="status">{tr('Loading inventory…')}</p>}
            >
              <InventoryPage
                user={user}
                branches={branches}
                timeZone={tenant.timeZone}
              />
            </Suspense>
          )}
          {error && <p role="alert">{error}</p>}
          {notice && <Notification key={noticeId} message={notice} />}
          {(tab === 'overview' || tab === 'settings') && (
            <>
              {tab === 'overview' && (
                <div className="overview-grid">
                  <section
                    className="card"
                    aria-labelledby="quick-actions-heading"
                  >
                    <h2 id="quick-actions-heading" className="card-title">
                      {tr('Quick actions')}
                    </h2>
                    <div className="quick-links">
                      {has('sales.create') && has('customers.view') && (
                        <Link to="/workspace/sales?new=1">
                          <PageIcon name="sales" size={20} />
                          <strong>{tr('New sales draft')}</strong>
                          <span>
                            {tr('Prepare a sale for review and posting')}
                          </span>
                        </Link>
                      )}
                      {has('customers.view') && (
                        <Link to="/workspace/customers">
                          <PageIcon name="customers" size={20} />
                          <strong>{tr('Customers')}</strong>
                          <span>
                            {tr('Profiles, balances and borrowed cylinders')}
                          </span>
                        </Link>
                      )}
                      {has('inventory.view') && (
                        <Link to="/workspace/inventory">
                          <PageIcon name="inventory" size={20} />
                          <strong>{tr('Stock')}</strong>
                          <span>
                            {tr('Balances and movements by warehouse')}
                          </span>
                        </Link>
                      )}
                      {has('finance.view') && (
                        <Link to="/workspace/cash-register">
                          <PageIcon name="cash-register" size={20} />
                          <strong>{tr('Cash register')}</strong>
                          <span>{tr('Open, count and close the drawer')}</span>
                        </Link>
                      )}
                      <Link to="/workspace/branches">
                        <PageIcon name="branches" size={20} />
                        <strong>{tr('Your branches')}</strong>
                        <span>{tr('Review locations and branch access')}</span>
                      </Link>
                      {has('users.manage') && (
                        <Link to="/workspace/team">
                          <PageIcon name="team" size={20} />
                          <strong>{tr('Your team')}</strong>
                          <span>
                            {tr('Invite colleagues and manage access')}
                          </span>
                        </Link>
                      )}
                      <Link to="/workspace/sessions">
                        <PageIcon name="sessions" size={20} />
                        <strong>{tr('Your sessions')}</strong>
                        <span>{tr('Review where you are signed in')}</span>
                      </Link>
                    </div>
                  </section>
                  <section
                    className="card"
                    aria-labelledby="workspace-details-heading"
                  >
                    <h2 id="workspace-details-heading" className="card-title">
                      {tr('Workspace details')}
                    </h2>
                    <KeyValues
                      items={[
                        ['Signed in as', user.email],
                        [
                          'Active branches',
                          `${branches.filter((b) => b.active).length} / ${tenant.maxBranches}`,
                        ],
                        [
                          'Active team members',
                          `${has('users.manage') ? members.filter((m) => m.active).length : '—'} / ${tenant.maxUsers}`,
                        ],
                        ['Administrator limit', tenant.maxAdmins],
                        ['Time zone', tenant.timeZone],
                      ]}
                    />
                  </section>
                </div>
              )}
              {tab === 'settings' && has('tenant.manage') && (
                <div className="settings-layout">
                  <form
                    className="card grid gap-4"
                    onSubmit={(e) => {
                      const f = data(e);
                      void run(() =>
                        mutate('tenant', 'PUT', {
                          name: f.get('name'),
                          timeZone: f.get('timeZone'),
                          overdueCreditDays: Number(f.get('overdueCreditDays')),
                          closingReminder: f.get('closingReminder'),
                        }),
                      );
                    }}
                  >
                    <h2 className="card-title">{tr('Business settings')}</h2>
                    <p className="section-help">
                      {tr(
                        'These details apply to every branch in this workspace.',
                      )}
                    </p>
                    <div className="form-grid">
                      <label>
                        {tr('Business name')}
                        <Input
                          name="name"
                          defaultValue={tenant.name}
                          maxLength={120}
                          required
                        />
                      </label>
                      <label>
                        {tr('Time zone')}
                        <Input
                          name="timeZone"
                          defaultValue={tenant.timeZone}
                          required
                        />
                      </label>
                      <label>
                        {tr('Credit is overdue after (days)')}
                        <Input
                          name="overdueCreditDays"
                          type="number"
                          min={1}
                          max={365}
                          defaultValue={tenant.overdueCreditDays}
                          required
                        />
                      </label>
                      <label>
                        {tr('Remind to close open registers at')}
                        <Input
                          name="closingReminder"
                          type="time"
                          defaultValue={tenant.closingReminder.slice(0, 5)}
                          required
                        />
                      </label>
                    </div>
                    <div>
                      <Button disabled={busy}>
                        {tr('Save business settings')}
                      </Button>
                    </div>
                  </form>
                </div>
              )}
            </>
          )}
          {tab === 'branches' && (
            <div className="split-layout">
              <section
                className="grid gap-4 content-start"
                aria-labelledby="branches-heading"
              >
                <div className="section-title">
                  <h2 id="branches-heading">{tr('Branches')}</h2>
                  <span className="toolbar-count">
                    {tr('{active} active of {limit} allowed', {
                      active: branches.filter((b) => b.active).length,
                      limit: tenant.maxBranches,
                    })}
                  </span>
                </div>
                <p className="section-help">
                  {tr('Branch access is assigned by a team administrator.')}
                </p>
                {branches.length === 0 && (
                  <PageState
                    kind="empty"
                    title={tr('No branches')}
                    description={tr('No branches have been assigned to you.')}
                  />
                )}
                <ul className="entity-grid">
                  {branches.map((branch) => (
                    <li
                      key={`${branch.id}-${branch.version}`}
                      className="entity-card"
                    >
                      <form
                        className="grid gap-3"
                        onSubmit={(e) => {
                          const f = data(e);
                          void run(() =>
                            mutate(`branches/${branch.id}`, 'PUT', {
                              name: f.get('name'),
                              active: f.has('active'),
                              version: branch.version,
                            }),
                          );
                        }}
                      >
                        <div className="entity-card-top">
                          <span
                            className="header-business-icon"
                            aria-hidden="true"
                          >
                            <PageIcon name="branches" size={20} />
                          </span>
                          <StatusChip
                            tone={branch.active ? 'success' : 'neutral'}
                          >
                            {branch.active ? tr('Active') : tr('Inactive')}
                          </StatusChip>
                        </div>
                        <label>
                          {tr('Branch name')}
                          <Input
                            name="name"
                            defaultValue={branch.name}
                            disabled={!has('branches.manage')}
                            required
                            maxLength={120}
                          />
                        </label>
                        <label className="check">
                          <Input
                            type="checkbox"
                            name="active"
                            defaultChecked={branch.active}
                            disabled={!has('branches.manage')}
                          />
                          {tr('Active')}
                        </label>
                        {has('branches.manage') && (
                          <div className="entity-actions">
                            <Button variant="outline" disabled={busy}>
                              {tr('Save branch')}
                            </Button>
                          </div>
                        )}
                      </form>
                    </li>
                  ))}
                </ul>
              </section>
              {has('branches.manage') && (
                <aside className="grid gap-6 content-start">
                  <form
                    className="card accent-card grid gap-4"
                    onSubmit={(e) => {
                      const f = data(e);
                      void run(() =>
                        mutate('branches', 'POST', { name: f.get('name') }),
                      );
                    }}
                  >
                    <h2 className="card-title">{tr('Add a branch')}</h2>
                    <label>
                      {tr('New branch name')}
                      <Input name="name" maxLength={120} required />
                    </label>
                    <Button disabled={busy} className="w-full">
                      {tr('Add branch')}
                    </Button>
                  </form>
                </aside>
              )}
            </div>
          )}
          {tab === 'team' && (
            <div className="split-layout">
              <section
                className="grid gap-4 content-start"
                aria-labelledby="team-heading"
              >
                <div className="section-title">
                  <h2 id="team-heading">{tr('Your team')}</h2>
                  <span className="toolbar-count">
                    {tr('{active} active of {limit} allowed', {
                      active: members.filter((m) => m.active).length,
                      limit: tenant.maxUsers,
                    })}
                  </span>
                </div>
                <ul className="member-list">
                  {members.map((member) => (
                    <li key={`${member.id}-${member.version}`}>
                      <MemberForm
                        member={member}
                        branches={branches}
                        roles={roles}
                        me={user}
                        busy={busy}
                        save={(body) =>
                          run(() => mutate(`members/${member.id}`, 'PUT', body))
                        }
                      />
                    </li>
                  ))}
                </ul>
              </section>
              <aside className="grid gap-6 content-start">
                <form
                  className="card accent-card grid gap-4"
                  onSubmit={(e) => {
                    const f = data(e);
                    void run(
                      () =>
                        mutate('members', 'POST', {
                          email: f.get('email'),
                          name: f.get('name'),
                          roleId: f.get('roleId'),
                          branchIds: f.getAll('branchIds'),
                        }),
                      'Team member added. An account email has been sent.',
                    );
                  }}
                >
                  <h2 className="card-title">{tr('Invite a team member')}</h2>
                  <label>
                    {tr('Name')}
                    <Input name="name" maxLength={120} required />
                  </label>
                  <label>
                    {tr('Email address')}
                    <Input name="email" type="email" required />
                  </label>
                  <label>
                    {tr('Role')}
                    <select
                      name="roleId"
                      defaultValue={roles.find((r) => r.name === 'Worker')?.id}
                    >
                      {roles
                        .filter(
                          (r) =>
                            !r.isOwner &&
                            r.permissions.every((p) =>
                              user.permissions.includes(p),
                            ),
                        )
                        .map((r) => (
                          <option key={r.id} value={r.id}>
                            {r.name}
                          </option>
                        ))}
                    </select>
                  </label>
                  <fieldset className="check-group">
                    <legend>{tr('Branch access')}</legend>
                    {branches
                      .filter((b) => b.active)
                      .map((b) => (
                        <label className="check" key={b.id}>
                          <Input
                            name="branchIds"
                            type="checkbox"
                            value={b.id}
                          />
                          {b.name}
                        </label>
                      ))}
                  </fieldset>
                  <Button disabled={busy} className="w-full">
                    {tr('Send invitation')}
                  </Button>
                </form>
              </aside>
            </div>
          )}
          {tab === 'roles' && (
            <section className="grid gap-4" aria-labelledby="roles-heading">
              <div className="section-title">
                <h2 id="roles-heading">{tr('Roles and permissions')}</h2>
              </div>
              <p className="section-help">
                {tr(
                  'Permissions are enforced by the server. Only the owner can define roles.',
                )}
              </p>
              <div className="role-grid">
                {roles.map((role) => (
                  <RoleForm
                    key={`${role.id}-${role.version}`}
                    role={role}
                    editable={user.isOwner && !role.isOwner}
                    busy={busy}
                    save={(body) =>
                      run(() => mutate(`roles/${role.id}`, 'PUT', body))
                    }
                  />
                ))}
                {user.isOwner && (
                  <RoleForm
                    editable
                    busy={busy}
                    save={(body) => run(() => mutate('roles', 'POST', body))}
                  />
                )}
              </div>
            </section>
          )}
          {tab === 'sessions' && (
            <section className="card" aria-labelledby="sessions-heading">
              <h2 id="sessions-heading" className="card-title">
                {tr('Your sessions in this workspace')}
              </h2>
              <p className="section-help">
                {tr(
                  'Sessions expire after 30 minutes of inactivity or eight hours. You can end them here.',
                )}
              </p>
              <ul className="session-list">
                {sessions.map((session) => (
                  <li key={session.id}>
                    <span className="header-business-icon" aria-hidden="true">
                      <PageIcon name="sessions" size={20} />
                    </span>
                    <div className="session-text">
                      <strong>
                        {session.current
                          ? tr('Current session')
                          : tr('Another session')}
                      </strong>
                      <span>
                        {tr('Started')}{' '}
                        {new Date(session.createdAt).toLocaleString(undefined, {
                          timeZone: tenant.timeZone,
                        })}
                      </span>
                    </div>
                    {session.current && (
                      <StatusChip tone="success">
                        {tr('This device')}
                      </StatusChip>
                    )}
                    <ConfirmButton
                      title={tr('End this session?')}
                      description={tr(
                        'The device using this session will need to sign in again.',
                      )}
                      destructive
                      disabled={busy}
                      onConfirm={() =>
                        void run(async () => {
                          await api(
                            `/api/auth/sessions/${session.id}`,
                            'DELETE',
                          );
                          if (session.current) {
                            client.clear();
                            window.location.assign('/login');
                          }
                        })
                      }
                    >
                      {tr('End session')}
                    </ConfirmButton>
                  </li>
                ))}
              </ul>
            </section>
          )}
          {tab === 'audit' && (
            <Suspense fallback={<p role="status">{tr('Loading activity…')}</p>}>
              <AuditPage
                user={user}
                branches={branches}
                timeZone={tenant.timeZone}
              />
            </Suspense>
          )}
          {tab === 'showcase' && (
            <Suspense
              fallback={
                <PageState
                  kind="loading"
                  title={tr('Loading components')}
                  description={tr('Opening the component showcase.')}
                />
              }
            >
              <ShowcasePage />
            </Suspense>
          )}
        </PageActionsSlot>
      )}
    </ApplicationShell>
  );
}

function MemberForm({
  member,
  roles,
  branches,
  me,
  busy,
  save,
}: {
  member: Member;
  roles: Role[];
  branches: Branch[];
  me: Me;
  busy: boolean;
  save: (body: unknown) => Promise<void>;
}) {
  const currentRole = roles.find((r) => r.id === member.roleId);
  const editable =
    member.id !== me.membershipId &&
    !currentRole?.isOwner &&
    currentRole?.permissions.every((p) => me.permissions.includes(p));
  return (
    <form
      className="member-card"
      onSubmit={(e) => {
        const f = data(e);
        void save({
          roleId: f.get('roleId'),
          active: f.has('active'),
          branchIds: f.getAll('branchIds'),
          version: member.version,
        });
      }}
    >
      <div className="member-head">
        <span className="avatar large" aria-hidden="true">
          {member.name
            .split(/\s+/)
            .filter(Boolean)
            .slice(0, 2)
            .map((part) => part[0]!.toUpperCase())
            .join('')}
        </span>
        <div className="member-identity">
          <h3>{member.name}</h3>
          <p>{member.email}</p>
        </div>
        <StatusChip tone={member.active ? 'success' : 'neutral'}>
          {member.active ? currentRole?.name || tr('Active') : tr('Inactive')}
        </StatusChip>
      </div>
      <label>
        {tr('Role')}
        <select name="roleId" defaultValue={member.roleId} disabled={!editable}>
          {roles
            .filter(
              (r) =>
                r.id === member.roleId ||
                (!r.isOwner &&
                  r.permissions.every((p) => me.permissions.includes(p))),
            )
            .map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
        </select>
      </label>
      <fieldset disabled={!editable} className="check-group">
        <legend>{tr('Branch access')}</legend>
        {branches
          .filter((b) => b.active)
          .map((b) => (
            <label key={b.id} className="check">
              <Input
                name="branchIds"
                type="checkbox"
                value={b.id}
                defaultChecked={member.branchIds.includes(b.id)}
              />
              {b.name}
            </label>
          ))}
      </fieldset>
      <label className="check">
        <Input
          name="active"
          type="checkbox"
          defaultChecked={member.active}
          disabled={!editable}
        />
        {tr('Active member')}
      </label>
      {editable && (
        <div>
          <Button variant="outline" disabled={busy}>
            {tr('Save member')}
          </Button>
        </div>
      )}
    </form>
  );
}

function RoleForm({
  role,
  editable,
  busy,
  save,
}: {
  role?: Role;
  editable: boolean;
  busy: boolean;
  save: (body: unknown) => Promise<void>;
}) {
  return (
    <form
      className={role ? 'role-card' : 'role-card new-role'}
      onSubmit={(e) => {
        const f = data(e);
        void save({
          name: f.get('name'),
          permissions: f.getAll('permissions'),
          version: role?.version || 0,
        });
      }}
    >
      <div className="member-head">
        <span className="header-business-icon" aria-hidden="true">
          <PageIcon name="roles" size={20} />
        </span>
        <h3>{role ? role.name : tr('Create a role')}</h3>
        {role?.isOwner && <StatusChip tone="info">{tr('Owner')}</StatusChip>}
      </div>
      <label>
        {tr('Role name')}
        <Input
          name="name"
          defaultValue={role?.name}
          maxLength={80}
          required
          disabled={!editable}
        />
      </label>
      <fieldset disabled={!editable} className="check-group permission-grid">
        <legend>{tr('Permissions')}</legend>
        {permissions.map((p) => (
          <label key={p} className="check">
            <Input
              type="checkbox"
              name="permissions"
              value={p}
              defaultChecked={role?.permissions.includes(p)}
            />
            {tr(permissionLabels[p])}
          </label>
        ))}
      </fieldset>
      {editable && (
        <Button disabled={busy}>
          {role ? tr('Save role') : tr('Create role')}
        </Button>
      )}
    </form>
  );
}
