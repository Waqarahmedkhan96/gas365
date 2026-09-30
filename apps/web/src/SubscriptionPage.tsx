import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import { PlanChooser } from './PlanChooser';
import { money } from './report-types';
import {
  dateText,
  daysLeft,
  type Onboarding,
  type Subscription,
  type SupportGrant,
} from './platform-types';

const statusText = {
  Trial: 'Free trial',
  Active: 'Active',
  Expired: 'Ended — read-only',
};

// Shown on the overview until every step is done or the owner hides it.
export function OnboardingChecklist({ user }: { user: Me }) {
  const cache = useQueryClient();
  const onboarding = useQuery({
    queryKey: ['onboarding', user.tenantId],
    queryFn: () =>
      api<Onboarding>(
        '/api/workspace/onboarding',
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const data = onboarding.data;
  if (!data || data.dismissed || data.steps.every((s) => s.done)) return null;
  const done = data.steps.filter((s) => s.done).length;
  const basicsDone = data.steps
    .filter((s) => s.key === 'warehouse' || s.key === 'cash')
    .every((s) => s.done);
  return (
    <>
      {!basicsDone && user.permissions.includes('tenant.manage') && (
        <section className="setup-hero" aria-labelledby="setup-hero-title">
          <div>
            <h2 id="setup-hero-title">Set up your business in 4 steps</h2>
            <p>
              Business details, your branch and store, the cash drawer and a
              plan. It takes about five minutes and every step can be skipped.
            </p>
          </div>
          <Button asChild>
            <Link to="/workspace/setup">Start setup</Link>
          </Button>
        </section>
      )}
      <section className="panel grid gap-3" aria-label="Getting started">
        <h2>Getting started</h2>
        <p className="section-help">
          {done} of {data.steps.length} steps done. Steps complete themselves as
          you set up the business.
        </p>
        <ol className="grid gap-2">
          {data.steps.map((s) => (
            <li key={s.key}>
              <strong>{s.done ? 'Done' : 'To do'}</strong> ·{' '}
              {s.done ? (
                s.label
              ) : (
                <Link to={`/workspace/${s.page}`}>{s.label}</Link>
              )}
            </li>
          ))}
        </ol>
        {user.permissions.includes('tenant.manage') && (
          <div>
            <Button
              variant="secondary"
              onClick={() =>
                void api(
                  '/api/workspace/onboarding',
                  'PUT',
                  { dismissed: true },
                  user.tenantId,
                ).then(() =>
                  cache.invalidateQueries({
                    queryKey: ['onboarding', user.tenantId],
                  }),
                )
              }
            >
              Hide checklist
            </Button>
          </div>
        )}
      </section>
    </>
  );
}

export function SubscriptionPage({
  user,
  timeZone,
}: {
  user: Me;
  timeZone: string;
}) {
  const cache = useQueryClient();
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const get = <T,>(path: string) =>
    api<T>('/api/workspace' + path, 'GET', undefined, user.tenantId);
  const subscription = useQuery({
    queryKey: ['subscription', user.tenantId],
    queryFn: () => get<Subscription>('/subscription'),
  });
  const grants = useQuery({
    queryKey: ['support', user.tenantId],
    queryFn: () => get<SupportGrant[]>('/support'),
  });
  async function run(action: () => Promise<unknown>, done: string) {
    if (busy) return;
    setBusy(true);
    setError('');
    setMessage('');
    try {
      await action();
      // Actions that return their own guidance (such as payment instructions) pass an empty message.
      if (done) setMessage(done);
      await cache.invalidateQueries({
        queryKey: ['subscription', user.tenantId],
      });
      await cache.invalidateQueries({ queryKey: ['support', user.tenantId] });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function grant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const d = new FormData(event.currentTarget);
    void run(
      () =>
        api(
          '/api/workspace/support',
          'POST',
          {
            hours: Number(d.get('hours')),
            includeFinance: d.has('includeFinance'),
            reason: String(d.get('reason') || ''),
          },
          user.tenantId,
        ),
      'Support access granted. You can revoke it at any time.',
    );
  }
  const s = subscription.data;
  const limits = s?.plan;
  return (
    <div className="grid gap-6">
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
      {subscription.error && <p role="alert">{subscription.error.message}</p>}
      {s && (
        <>
          <section className="panel plan-hero grid gap-3">
            <h2>Your plan</h2>
            <p className="plan-hero-line">
              <strong>{s.plan?.name ?? 'No plan'}</strong> ·{' '}
              {statusText[s.status]}
              {s.status === 'Trial' &&
                s.trialEndsAt &&
                ` · ${daysLeft(s.trialEndsAt)} days left (ends ${dateText(s.trialEndsAt, timeZone)})`}
              {s.status === 'Active' &&
                (s.paidThrough
                  ? ` · paid through ${dateText(s.paidThrough, timeZone)}`
                  : ' · pilot access with no end date')}
            </p>
            {s.status === 'Expired' && (
              <p>
                Your data is safe and remains readable and exportable. Choose a
                plan below to make changes again.
              </p>
            )}
            {s.requestedPlan && (
              <p>
                Requested: <strong>{s.requestedPlan.name}</strong>
                {s.requestedBillingPeriod === 'Yearly'
                  ? ' (billed yearly)'
                  : ' (billed monthly)'}
                . The Gas365 team activates it after confirming your payment.
              </p>
            )}
          </section>
          <section className="panel grid gap-3">
            <h2>Usage in {s.usage.month}</h2>
            <div
              className="data-table report-table overflow-x-auto"
              role="region"
              aria-label="Usage table"
              tabIndex={0}
            >
              <table>
                <caption>Usage against your plan limits</caption>
                <thead>
                  <tr>
                    <th scope="col">Measure</th>
                    <th scope="col" className="text-right">
                      This month / now
                    </th>
                    <th scope="col" className="text-right">
                      Plan limit
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {[
                    ['Active branches', s.usage.branches, limits?.maxBranches],
                    ['Active users', s.usage.users, limits?.maxUsers],
                    ['Administrators', s.usage.admins, limits?.maxAdmins],
                    ['Sales invoices', s.usage.salesInvoices, null],
                    ['Purchase invoices', s.usage.purchaseInvoices, null],
                    ['Finance entries', s.usage.financeEntries, null],
                    ['Stock documents', s.usage.stockDocuments, null],
                    ['Exports', s.usage.exports, null],
                    ['Imported rows', s.usage.importedRows, null],
                  ].map(([label, value, limit]) => (
                    <tr key={String(label)}>
                      <td>{label}</td>
                      <td className="text-right tabular-nums">{value}</td>
                      <td className="text-right tabular-nums">
                        {limit ?? 'No limit'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
          <section className="panel grid gap-3">
            <PlanChooser user={user} />
          </section>
          <section className="panel grid gap-3">
            <h2>Billing history</h2>
            <ul className="setup-list">
              {s.history.map((h) => (
                <li key={h.id}>
                  {dateText(h.occurredAt, timeZone)} ·{' '}
                  {h.kind.replace(/([a-z])([A-Z])/g, '$1 $2')}
                  {h.planName && ` · ${h.planName}`}
                  {h.amount !== null && ` · ${money(h.amount)}`}
                  {h.reference && ` · ${h.reference}`}
                  {h.note && ` · ${h.note}`}
                </li>
              ))}
            </ul>
          </section>
        </>
      )}
      <section className="panel grid gap-3">
        <h2>Support access</h2>
        <p className="section-help">
          The Gas365 team can only look inside your business when you allow it.
          Access is read-only, ends automatically, and every page they view is
          recorded in your activity log.
        </p>
        <form className="grid gap-3" onSubmit={grant}>
          <label className="grid gap-1">
            Allow access for
            <select name="hours" defaultValue="4">
              <option value="1">1 hour</option>
              <option value="4">4 hours</option>
              <option value="24">24 hours</option>
              <option value="72">3 days</option>
            </select>
          </label>
          <label className="flex items-center gap-2">
            <input type="checkbox" name="includeFinance" />
            Include cash, credit and profit figures
          </label>
          <label className="grid gap-1">
            What do you need help with?
            <input name="reason" required maxLength={300} />
          </label>
          <Button disabled={busy}>Allow support access</Button>
        </form>
        <ul className="grid gap-2">
          {grants.data?.map((g) => (
            <li key={g.id} className="record-row">
              <span>
                {g.active ? 'Active' : 'Ended'} · until{' '}
                {new Date(g.expiresAt).toLocaleString('en-PK', { timeZone })} ·{' '}
                {g.includeFinance
                  ? 'including financial figures'
                  : 'operational data only'}{' '}
                · {g.reason}
              </span>
              {g.active && (
                <Button
                  variant="secondary"
                  onClick={() =>
                    void run(
                      () =>
                        api(
                          `/api/workspace/support/${g.id}/revoke`,
                          'POST',
                          undefined,
                          user.tenantId,
                        ),
                      'Support access revoked.',
                    )
                  }
                >
                  Revoke
                </Button>
              )}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
