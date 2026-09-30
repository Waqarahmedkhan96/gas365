import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import { StatusChip } from './components/page';
import { money } from './report-types';
import type { BillingPeriod, Subscription } from './platform-types';

const count = (n: number, one: string, many: string) =>
  `${n} ${n === 1 ? one : many}`;

// Plan cards with a monthly/yearly switch. All prices, including yearly amounts and savings, come from the server.
export function PlanChooser({
  user,
  heading = 'Plans',
}: {
  user: Me;
  heading?: string;
}) {
  const cache = useQueryClient();
  const [period, setPeriod] = useState<BillingPeriod | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const subscription = useQuery({
    queryKey: ['subscription', user.tenantId],
    queryFn: () =>
      api<Subscription>(
        '/api/workspace/subscription',
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const s = subscription.data;
  if (subscription.error)
    return (
      <p role="alert" className="error-banner">
        {subscription.error.message}
      </p>
    );
  if (!s) return <p role="status">Loading plans…</p>;
  const billing: BillingPeriod =
    period ?? s.requestedBillingPeriod ?? 'Monthly';
  async function request(planId: string) {
    if (busy) return;
    setBusy(true);
    setError('');
    setMessage('');
    try {
      const r = await api<{ instructions: string }>(
        '/api/workspace/subscription/request',
        'POST',
        { planId, billingPeriod: billing },
        user.tenantId,
      );
      setMessage(r.instructions);
      await cache.invalidateQueries({
        queryKey: ['subscription', user.tenantId],
      });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="grid gap-3">
      <div className="plans-header">
        <h2>{heading}</h2>
        <div
          className="billing-toggle"
          role="radiogroup"
          aria-label="Billing period"
        >
          {(['Monthly', 'Yearly'] as const).map((value) => (
            <button
              key={value}
              type="button"
              role="radio"
              aria-checked={billing === value}
              onClick={() => setPeriod(value)}
            >
              {value}
              {value === 'Yearly' && (
                <span className="billing-badge">2 months free</span>
              )}
            </button>
          ))}
        </div>
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
      <ul className="plan-grid">
        {s.plans.map((p) => {
          const requested =
            s.requestedPlan?.id === p.id &&
            s.requestedBillingPeriod === billing;
          return (
            <li
              key={p.id}
              className={
                s.plan?.id === p.id ? 'plan-card current' : 'plan-card'
              }
            >
              <h3>{p.name}</h3>
              <p className="plan-price">
                {p.monthlyPrice === null
                  ? 'Price on request'
                  : billing === 'Yearly' && p.yearlyPrice !== null
                    ? `${money(p.yearlyPrice)} per year`
                    : `${money(p.monthlyPrice)} per month`}
              </p>
              {billing === 'Yearly' && p.yearlySaving !== null && (
                <p className="plan-saving">
                  You save {money(p.yearlySaving)} (2 months free)
                </p>
              )}
              <ul className="plan-features">
                <li>{count(p.maxBranches, 'branch', 'branches')}</li>
                <li>{count(p.maxUsers, 'user', 'users')}</li>
                <li>{count(p.maxAdmins, 'administrator', 'administrators')}</li>
              </ul>
              {s.plan?.id === p.id && s.status === 'Active' ? (
                <p>
                  <StatusChip tone="success">Current plan</StatusChip>
                </p>
              ) : (
                <Button
                  className="w-full"
                  disabled={busy || requested}
                  onClick={() => void request(p.id)}
                >
                  {requested
                    ? 'Requested'
                    : billing === 'Yearly'
                      ? `Choose ${p.name} yearly`
                      : `Choose ${p.name}`}
                </Button>
              )}
            </li>
          );
        })}
      </ul>
      <p className="section-help">
        Payments are confirmed by the Gas365 team ({s.paymentProvider}).
      </p>
    </div>
  );
}
