import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, Navigate } from 'react-router-dom';
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

const permissions = [
  'tenant.manage',
  'branches.manage',
  'users.manage',
  'roles.manage',
  'audit.view',
];
const permissionLabels: Record<string, string> = {
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
  const [tab, setTab] = useState('overview');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const user = me.data;
  const has = (permission: string) => user?.permissions.includes(permission);
  const info = useQuery({
    queryKey: ['workspace', user?.tenantId],
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
    } catch (failure) {
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
  if (!user || !info.data)
    return (
      <main>
        <h1>Your workspace</h1>
        <p role={me.isError || info.isError ? 'alert' : 'status'}>
          {me.error?.message ||
            info.error?.message ||
            'Loading your workspace…'}
        </p>
        <Link to="/login">Sign in</Link>
        <button
          onClick={() => {
            void me.refetch();
            void info.refetch();
          }}
        >
          Try again
        </button>
      </main>
    );
  const { tenant, branches, roles, members, sessions, audit } = info.data;

  return (
    <>
      <header>
        <Link to="/" className="brand">
          Gas<span>365</span>
        </Link>
        <div className="header-actions">
          <span>{user.name}</span>
          <button
            className="secondary"
            disabled={busy}
            onClick={() =>
              void run(async () => {
                await api('/api/auth/logout', 'POST');
                client.clear();
                window.location.assign('/login');
              })
            }
          >
            Sign out
          </button>
        </div>
      </header>
      <main className="workspace-page">
        <div className="workspace-heading">
          <div>
            <p className="eyebrow">WORKSPACE</p>
            <h1>{tenant.name}</h1>
          </div>
          {user.workspaces.length > 1 && (
            <label>
              Workspace
              <select
                value={user.membershipId}
                disabled={busy}
                onChange={(e) =>
                  void run(async () => {
                    await api('/api/auth/switch', 'POST', {
                      membershipId: e.target.value,
                    });
                    client.clear();
                    window.location.assign('/workspace');
                  })
                }
              >
                {user.workspaces.map((w) => (
                  <option key={w.membershipId} value={w.membershipId}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
        <nav className="tabs" aria-label="Workspace sections">
          {[
            ['overview', 'Overview'],
            ['branches', 'Branches'],
            ...(has('users.manage')
              ? [
                  ['team', 'Team'],
                  ['roles', 'Roles'],
                ]
              : []),
            ['sessions', 'Sessions'],
            ...(has('audit.view') ? [['audit', 'Activity']] : []),
          ].map(([id, label]) => (
            <button
              key={id}
              className={tab === id ? '' : 'secondary'}
              aria-pressed={tab === id}
              onClick={() => {
                setTab(id);
                setError('');
                setNotice('');
              }}
            >
              {label}
            </button>
          ))}
        </nav>
        {error && <p role="alert">{error}</p>}
        {notice && (
          <p role="status" className="success-text">
            {notice}
          </p>
        )}
        {tab === 'overview' && (
          <>
            <section className="status-card">
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
            {has('tenant.manage') && (
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
                  <input
                    name="name"
                    defaultValue={tenant.name}
                    maxLength={120}
                    required
                  />
                </label>
                <label>
                  Time zone
                  <input
                    name="timeZone"
                    defaultValue={tenant.timeZone}
                    required
                  />
                </label>
                <button disabled={busy}>Save business settings</button>
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
                    <input
                      name="name"
                      defaultValue={branch.name}
                      disabled={!has('branches.manage')}
                      required
                      maxLength={120}
                    />
                  </label>
                  <label className="check">
                    <input
                      type="checkbox"
                      name="active"
                      defaultChecked={branch.active}
                      disabled={!has('branches.manage')}
                    />
                    Active
                  </label>
                  {has('branches.manage') && (
                    <button disabled={busy}>Save branch</button>
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
                  <input name="name" maxLength={120} required />
                </label>
                <button disabled={busy}>Add branch</button>
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
                <input name="name" maxLength={120} required />
              </label>
              <label>
                Email address
                <input name="email" type="email" required />
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
                      <input name="branchIds" type="checkbox" value={b.id} />
                      {b.name}
                    </label>
                  ))}
              </fieldset>
              <button disabled={busy}>Send invitation</button>
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
              Permissions are enforced by the server. Only the owner can define
              roles.
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
              Sessions expire after 30 minutes of inactivity or eight hours. You
              can end them here.
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
                <button
                  className="secondary"
                  disabled={busy}
                  onClick={() =>
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
                </button>
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
      </main>
    </>
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
              <input
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
        <input
          name="active"
          type="checkbox"
          defaultChecked={member.active}
          disabled={!editable}
        />
        Active member
      </label>
      {editable && <button disabled={busy}>Save member</button>}
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
        <input
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
            <input
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
        <button disabled={busy}>{role ? 'Save role' : 'Create role'}</button>
      )}
    </form>
  );
}
