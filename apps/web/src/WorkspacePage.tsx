import { lazy, Suspense, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, Navigate, useParams } from 'react-router-dom';
import { ApplicationShell } from './components/ApplicationShell';
import { PageHeading, PageState } from './components/page';
import { Button } from './components/ui/button';
import { Input } from './components/ui/form';
import { ConfirmButton, Notification } from './components/ui/feedback';
import { canVisit, pages } from './navigation';
import {
  api,
  ApiError,
  type Me,
  type Tenant,
  type Branch,
  type Role,
  type Member,
  type Session,
  type Audit,
} from './access-api';

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
  const user = me.data;
  const has = (permission: string) => user?.permissions.includes(permission);
  const info = useQuery({
    queryKey: ['workspace', user?.tenantId, user?.permissions.join(',')],
    enabled: !!user,
    retry: false,
    queryFn: async () => {
      const get = <T,>(path: string) =>
        api<T>(`/api/workspace/${path}`, 'GET', undefined, user!.tenantId);
      const [tenant, branches, roles, members, sessions, audit] =
        await Promise.all([
          get<Tenant>('tenant'),
          get<Branch[]>('branches'),
          has('users.manage') ? get<Role[]>('roles') : [],
          has('users.manage') ? get<Member[]>('members') : [],
          api<Session[]>('/api/auth/sessions'),
          has('audit.view') ? get<Audit[]>('audit') : [],
        ]);
      return { tenant, branches, roles, members, sessions, audit };
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
        <h1>Your workspace</h1>
        <PageState
          kind={me.isError || info.isError ? 'error' : 'loading'}
          title={
            me.isError || info.isError
              ? 'Workspace unavailable'
              : 'Loading your workspace'
          }
          description={
            me.error?.message ||
            info.error?.message ||
            'Retrieving your workspace and permissions.'
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
        <Link to="/login">Sign in</Link>
      </main>
    );
  const { tenant, branches, roles, members, sessions, audit } = info.data;

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
      <PageHeading
        title={currentPage?.label || 'Page not found'}
        description={currentPage?.description || 'This page is not available.'}
      />
      {!currentPage ? (
        <PageState
          kind="empty"
          title="Page not found"
          description="Choose a page from the workspace navigation."
        />
      ) : !allowed ? (
        <PageState
          kind="denied"
          title="Access restricted"
          description="Your role does not include access to this page. Ask your workspace owner if you need it."
        />
      ) : (
        <>
          {(tab === 'purchases' || tab === 'suppliers') && (
            <Suspense fallback={<p role="status">Loading purchasing…</p>}>
              <PurchasingPage user={user} suppliersOnly={tab === 'suppliers'} />
            </Suspense>
          )}
          {tab === 'inventory' && (
            <Suspense fallback={<p role="status">Loading inventory…</p>}>
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
                <section className="status-card overview-card">
                  <h2>Workspace details</h2>
                  <p>{user.email}</p>
                  <div className="summary-grid">
                    <div>
                      <strong>
                        {branches.filter((b) => b.active).length} /{' '}
                        {tenant.maxBranches}
                      </strong>
                      <span>Active branches</span>
                    </div>
                    <div>
                      <strong>
                        {has('users.manage')
                          ? members.filter((m) => m.active).length
                          : '—'}{' '}
                        / {tenant.maxUsers}
                      </strong>
                      <span>Active team members</span>
                    </div>
                    <div>
                      <strong>{tenant.maxAdmins}</strong>
                      <span>Administrator limit</span>
                    </div>
                  </div>
                </section>
              )}
              {tab === 'overview' && (
                <section className="panel">
                  <h2>Make this workspace yours</h2>
                  <p className="section-help">
                    Keep your locations and team access up to date.
                  </p>
                  <div className="quick-links">
                    <Link to="/workspace/branches">
                      <strong>Your branches</strong>
                      <span>Review locations and branch access →</span>
                    </Link>
                    {has('users.manage') && (
                      <Link to="/workspace/team">
                        <strong>Your team</strong>
                        <span>Invite colleagues and manage access →</span>
                      </Link>
                    )}
                    <Link to="/workspace/sessions">
                      <strong>Your sessions</strong>
                      <span>Review where you are signed in →</span>
                    </Link>
                  </div>
                </section>
              )}
              {tab === 'settings' && has('tenant.manage') && (
                <form
                  className="status-card stack"
                  onSubmit={(e) => {
                    const f = data(e);
                    void run(() =>
                      mutate('tenant', 'PUT', {
                        name: f.get('name'),
                        timeZone: f.get('timeZone'),
                      }),
                    );
                  }}
                >
                  <h2>Business settings</h2>
                  <label>
                    Business name
                    <Input
                      name="name"
                      defaultValue={tenant.name}
                      maxLength={120}
                      required
                    />
                  </label>
                  <label>
                    Time zone
                    <Input
                      name="timeZone"
                      defaultValue={tenant.timeZone}
                      required
                    />
                  </label>
                  <Button disabled={busy}>Save business settings</Button>
                </form>
              )}
            </>
          )}
          {tab === 'branches' && (
            <>
              <section className="status-card">
                <h2>Branches</h2>
                <p>Branch access is assigned by a team administrator.</p>
                {branches.length === 0 && (
                  <p>No branches have been assigned to you.</p>
                )}
                {branches.map((branch) => (
                  <form
                    key={`${branch.id}-${branch.version}`}
                    className="record-row"
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
                    <label>
                      Branch name
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
                      Active
                    </label>
                    {has('branches.manage') && (
                      <Button disabled={busy}>Save branch</Button>
                    )}
                  </form>
                ))}
              </section>
              {has('branches.manage') && (
                <form
                  className="status-card stack"
                  onSubmit={(e) => {
                    const f = data(e);
                    void run(() =>
                      mutate('branches', 'POST', { name: f.get('name') }),
                    );
                  }}
                >
                  <h2>Add a branch</h2>
                  <label>
                    New branch name
                    <Input name="name" maxLength={120} required />
                  </label>
                  <Button disabled={busy}>Add branch</Button>
                </form>
              )}
            </>
          )}
          {tab === 'team' && (
            <>
              <form
                className="status-card stack"
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
                <h2>Invite a team member</h2>
                <label>
                  Name
                  <Input name="name" maxLength={120} required />
                </label>
                <label>
                  Email address
                  <Input name="email" type="email" required />
                </label>
                <label>
                  Role
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
                <fieldset>
                  <legend>Branch access</legend>
                  {branches
                    .filter((b) => b.active)
                    .map((b) => (
                      <label className="check" key={b.id}>
                        <Input name="branchIds" type="checkbox" value={b.id} />
                        {b.name}
                      </label>
                    ))}
                </fieldset>
                <Button disabled={busy}>Send invitation</Button>
              </form>
              <section className="status-card">
                <h2>Your team</h2>
                {members.map((member) => (
                  <MemberForm
                    key={`${member.id}-${member.version}`}
                    member={member}
                    branches={branches}
                    roles={roles}
                    me={user}
                    busy={busy}
                    save={(body) =>
                      run(() => mutate(`members/${member.id}`, 'PUT', body))
                    }
                  />
                ))}
              </section>
            </>
          )}
          {tab === 'roles' && (
            <section className="status-card stack">
              <h2>Roles and permissions</h2>
              <p>
                Permissions are enforced by the server. Only the owner can
                define roles.
              </p>
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
            </section>
          )}
          {tab === 'sessions' && (
            <section className="status-card">
              <h2>Your sessions in this workspace</h2>
              <p>
                Sessions expire after 30 minutes of inactivity or eight hours.
                You can end them here.
              </p>
              {sessions.map((session) => (
                <div className="record-row" key={session.id}>
                  <div>
                    <strong>
                      {session.current ? 'Current session' : 'Another session'}
                    </strong>
                    <p>
                      Started{' '}
                      {new Date(session.createdAt).toLocaleString(undefined, {
                        timeZone: tenant.timeZone,
                      })}
                    </p>
                  </div>
                  <ConfirmButton
                    title="End this session?"
                    description="The device using this session will need to sign in again."
                    destructive

                    disabled={busy}
                    onConfirm={() =>
                      void run(async () => {
                        await api(`/api/auth/sessions/${session.id}`, 'DELETE');
                        if (session.current) {
                          client.clear();
                          window.location.assign('/login');
                        }
                      })
                    }
                  >
                    End session
                  </ConfirmButton>
                </div>
              ))}
            </section>
          )}
          {tab === 'audit' && (
            <section className="status-card">
              <h2>Recent activity</h2>
              <p>The latest 100 events in this workspace.</p>
              {audit.map((item) => (
                <div className="record-row" key={item.id}>
                  <strong>{item.action.replaceAll('.', ' ')}</strong>
                  <time dateTime={item.occurredAt}>
                    {new Date(item.occurredAt).toLocaleString(undefined, {
                      timeZone: tenant.timeZone,
                    })}
                  </time>
                </div>
              ))}
            </section>
          )}
          {tab === 'showcase' && (
            <Suspense
              fallback={
                <PageState
                  kind="loading"
                  title="Loading components"
                  description="Opening the component showcase."
                />
              }
            >
              <ShowcasePage />
            </Suspense>
          )}
        </>
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
      className="record-editor"
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
      <h3>{member.name}</h3>
      <p>{member.email}</p>
      <label>
        Role
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
      <fieldset disabled={!editable}>
        <legend>Branch access</legend>
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
        Active member
      </label>
      {editable && <Button disabled={busy}>Save member</Button>}
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
      className="record-editor stack"
      onSubmit={(e) => {
        const f = data(e);
        void save({
          name: f.get('name'),
          permissions: f.getAll('permissions'),
          version: role?.version || 0,
        });
      }}
    >
      <h3>{role ? role.name : 'Create a role'}</h3>
      <label>
        Role name
        <Input
          name="name"
          defaultValue={role?.name}
          maxLength={80}
          required
          disabled={!editable}
        />
      </label>
      <fieldset disabled={!editable}>
        <legend>Permissions</legend>
        {permissions.map((p) => (
          <label key={p} className="check">
            <Input
              type="checkbox"
              name="permissions"
              value={p}
              defaultChecked={role?.permissions.includes(p)}
            />
            {permissionLabels[p]}
          </label>
        ))}
      </fieldset>
      {editable && (
        <Button disabled={busy}>{role ? 'Save role' : 'Create role'}</Button>
      )}
    </form>
  );
}
