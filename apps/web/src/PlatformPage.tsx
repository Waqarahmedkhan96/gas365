import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './access-api';
import { Button } from './components/ui/button';
import {
  dateText,
  type Plan,
  type SupportGrant,
  type TenantSummary,
} from './platform-types';

const statusText = { Trial: 'Trial', Active: 'Active', Expired: 'Ended' };

// Platform routes use the user's identity only, so no X-Workspace header is sent.
const platform = <T,>(path: string, method = 'GET', body?: unknown) =>
  api<T>('/api/platform' + path, method, body);

export function PlatformPage({ timeZone }: { timeZone: string }) {
  const cache = useQueryClient();
  const [view, setView] = useState<'tenants' | 'plans' | 'support'>('tenants');
  const [search, setSearch] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const tenants = useQuery({
    queryKey: ['platform', 'tenants', search],
    queryFn: () =>
      platform<{ items: TenantSummary[]; total: number }>(
        `/tenants?search=${encodeURIComponent(search)}`,
      ),
    enabled: view === 'tenants',
  });
  const plans = useQuery({
    queryKey: ['platform', 'plans'],
    queryFn: () => platform<Plan[]>('/plans'),
  });
  const grants = useQuery({
    queryKey: ['platform', 'grants'],
    queryFn: () => platform<SupportGrant[]>('/support-grants'),
    enabled: view === 'support',
  });
  async function run(action: () => Promise<unknown>, done: string) {
    setError('');
    setMessage('');
    try {
      await action();
      if (done) setMessage(done);
      await cache.invalidateQueries({ queryKey: ['platform'] });
    } catch (e) {
      setError((e as Error).message);
    }
  }
  const submit =
    (fn: (d: FormData) => Promise<unknown>, done: string) =>
    (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      const d = new FormData(event.currentTarget);
      void run(() => fn(d), done);
    };
  return (
    <div className="grid gap-6">
      <p className="section-help">
        Platform administration shows plans, status and usage counts only.
        Business records are visible only through an owner-approved support
        view.
      </p>
      <div className="flex flex-wrap gap-3" role="group" aria-label="View">
        {(
          [
            ['tenants', 'Businesses'],
            ['plans', 'Plans'],
            ['support', 'Support requests'],
          ] as const
        ).map(([key, label]) => (
          <Button
            key={key}
            variant={view === key ? 'default' : 'secondary'}
            aria-pressed={view === key}
            onClick={() => setView(key)}
          >
            {label}
          </Button>
        ))}
      </div>
      {message && (
        <p role="status" className="notice-banner">
          {message}
        </p>
      )}
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {view === 'tenants' && (
        <section className="panel grid gap-4">
          <label className="grid gap-1">
            Search businesses
            <input value={search} onChange={(e) => setSearch(e.target.value)} />
          </label>
          {tenants.error && <p role="alert">{tenants.error.message}</p>}
          <ul className="grid gap-4">
            {tenants.data?.items.map((t) => (
              <li key={t.tenantId} className="status-card grid gap-3">
                <div>
                  <h2>{t.name}</h2>
                  <p>
                    {t.plan ?? 'No plan'} · {statusText[t.status]}
                    {t.status === 'Trial' &&
                      ` until ${dateText(t.trialEndsAt, timeZone)}`}
                    {t.paidThrough &&
                      ` · paid through ${dateText(t.paidThrough, timeZone)}`}
                    {t.requestedPlan && ` · requested ${t.requestedPlan}`}
                  </p>
                  <p className="section-help">
                    {t.users} users · {t.branches} branches ·{' '}
                    {t.transactionsThisMonth} transactions this month · since{' '}
                    {dateText(t.createdAt, timeZone)}
                  </p>
                </div>
                <details>
                  <summary>Record payment and activate</summary>
                  <form
                    className="grid gap-3"
                    onSubmit={submit(
                      (d) =>
                        platform(`/tenants/${t.tenantId}/activate`, 'POST', {
                          planId: d.get('planId'),
                          months: Number(d.get('months')),
                          amount: Number(d.get('amount')),
                          reference: d.get('reference'),
                        }),
                      `${t.name} activated.`,
                    )}
                  >
                    <label className="grid gap-1">
                      Plan
                      <select
                        name="planId"
                        defaultValue={
                          plans.data?.find((p) => p.name === t.requestedPlan)
                            ?.id
                        }
                      >
                        {plans.data
                          ?.filter((p) => p.active)
                          .map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.name}
                            </option>
                          ))}
                      </select>
                    </label>
                    <label className="grid gap-1">
                      Months
                      <input
                        name="months"
                        type="number"
                        min={1}
                        max={24}
                        defaultValue={1}
                        required
                      />
                    </label>
                    <label className="grid gap-1">
                      Amount received (PKR)
                      <input
                        name="amount"
                        type="number"
                        min={0}
                        step="0.01"
                        required
                      />
                    </label>
                    <label className="grid gap-1">
                      Payment reference
                      <input name="reference" required maxLength={120} />
                    </label>
                    <Button>Activate plan</Button>
                  </form>
                </details>
                {t.status !== 'Active' && (
                  <details>
                    <summary>Extend trial</summary>
                    <form
                      className="grid gap-3"
                      onSubmit={submit(
                        (d) =>
                          platform(
                            `/tenants/${t.tenantId}/extend-trial`,
                            'POST',
                            {
                              days: Number(d.get('days')),
                              reason: d.get('reason'),
                            },
                          ),
                        `${t.name}'s trial extended.`,
                      )}
                    >
                      <label className="grid gap-1">
                        Extra days
                        <input
                          name="days"
                          type="number"
                          min={1}
                          max={30}
                          defaultValue={7}
                          required
                        />
                      </label>
                      <label className="grid gap-1">
                        Reason
                        <input name="reason" required maxLength={300} />
                      </label>
                      <Button variant="secondary">Extend trial</Button>
                    </form>
                  </details>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}
      {view === 'plans' && (
        <section className="grid gap-4">
          {plans.data?.map((p) => (
            <form
              key={p.id}
              className="panel grid gap-3"
              onSubmit={submit(
                (d) =>
                  platform(`/plans/${p.id}`, 'PUT', {
                    name: d.get('name'),
                    monthlyPrice:
                      d.get('price') === '' ? null : Number(d.get('price')),
                    maxBranches: Number(d.get('branches')),
                    maxUsers: Number(d.get('users')),
                    maxAdmins: Number(d.get('admins')),
                    active: d.has('active'),
                    version: p.version,
                  }),
                `${p.name} plan saved.`,
              )}
            >
              <h2>{p.name}</h2>
              <label className="grid gap-1">
                Plan name
                <input
                  name="name"
                  defaultValue={p.name}
                  required
                  maxLength={60}
                />
              </label>
              <label className="grid gap-1">
                Monthly price (PKR, blank for price on request)
                <input
                  name="price"
                  type="number"
                  min={0}
                  step="0.01"
                  defaultValue={p.monthlyPrice ?? ''}
                />
              </label>
              <div className="flex flex-wrap gap-4">
                <label className="grid gap-1">
                  Branches
                  <input
                    name="branches"
                    type="number"
                    min={1}
                    defaultValue={p.maxBranches}
                    required
                  />
                </label>
                <label className="grid gap-1">
                  Users
                  <input
                    name="users"
                    type="number"
                    min={1}
                    defaultValue={p.maxUsers}
                    required
                  />
                </label>
                <label className="grid gap-1">
                  Administrators
                  <input
                    name="admins"
                    type="number"
                    min={1}
                    defaultValue={p.maxAdmins}
                    required
                  />
                </label>
              </div>
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  name="active"
                  defaultChecked={p.active}
                />
                Offered to businesses
              </label>
              <Button variant="secondary">Save {p.name}</Button>
            </form>
          ))}
        </section>
      )}
      {view === 'support' && (
        <section className="panel grid gap-3">
          <h2>Active support grants</h2>
          {grants.data?.length === 0 && (
            <p>No business has granted support access right now.</p>
          )}
          <ul className="grid gap-2">
            {grants.data?.map((g) => (
              <li key={g.id} className="record-row">
                <span>
                  <strong>{g.tenantName}</strong> · until{' '}
                  {new Date(g.expiresAt).toLocaleString('en-PK', { timeZone })}{' '}
                  ·{' '}
                  {g.includeFinance
                    ? 'including financial figures'
                    : 'operational data only'}{' '}
                  · {g.reason}
                </span>
                <Button
                  onClick={() =>
                    void run(async () => {
                      await platform(`/support/${g.id}/enter`, 'POST');
                      window.location.assign('/workspace');
                    }, '')
                  }
                >
                  Open read-only view
                </Button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
