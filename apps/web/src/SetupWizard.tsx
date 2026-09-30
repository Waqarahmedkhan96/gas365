import { useState, type FormEvent, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { Building2, Check, PartyPopper, Wallet } from 'lucide-react';
import { api, type Branch, type Me, type Tenant } from './access-api';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { PlanChooser } from './PlanChooser';

const steps = [
  { key: 'business', label: 'Business details' },
  { key: 'branch', label: 'Branch and store' },
  { key: 'cash', label: 'Cash drawer' },
  { key: 'plan', label: 'Choose a plan' },
] as const;

function Stepper({ current }: { current: number }) {
  return (
    <ol className="stepper" aria-label="Setup progress">
      {steps.map((step, i) => {
        const state = i < current ? 'done' : i === current ? 'current' : 'next';
        return (
          <li
            key={step.key}
            className={state}
            aria-current={i === current ? 'step' : undefined}
          >
            <span className="stepper-dot" aria-hidden="true">
              {i < current ? <Check size={16} /> : i + 1}
            </span>
            <span className="stepper-label">
              {step.label}
              <span className="sr-only">
                {state === 'done'
                  ? ' (completed)'
                  : state === 'next'
                    ? ' (not started)'
                    : ''}
              </span>
            </span>
          </li>
        );
      })}
    </ol>
  );
}

function StepCard({
  title,
  description,
  icon,
  children,
}: {
  title: string;
  description: string;
  icon: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="card setup-step" aria-labelledby="setup-step-title">
      <div className="setup-step-head">
        <span className="report-intro-icon" aria-hidden="true">
          {icon}
        </span>
        <div>
          <h2 id="setup-step-title" className="card-title">
            {title}
          </h2>
          <p className="section-help">{description}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

// Guided first-time setup. Every step uses the ordinary workspace APIs and can be skipped.
export function SetupWizard({
  user,
  tenant,
  branches,
}: {
  user: Me;
  tenant: Tenant;
  branches: Branch[];
}) {
  const cache = useQueryClient();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const branch = branches.find((b) => b.active);
  const can = (p: string) => user.permissions.includes(p);
  const next = () => {
    setError('');
    setStep((s) => s + 1);
    window.scrollTo({ top: 0 });
  };
  async function save(
    event: FormEvent<HTMLFormElement>,
    action: (d: FormData) => Promise<unknown>,
  ) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError('');
    try {
      await action(data);
      await cache.invalidateQueries();
      next();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const skip = (
    <Button type="button" variant="ghost" onClick={next} disabled={busy}>
      Skip for now
    </Button>
  );
  const text = (d: FormData, key: string) => String(d.get(key) ?? '');
  return (
    <div className="setup-wizard">
      <Stepper current={step} />
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {step === 0 && (
        <StepCard
          title="Business details"
          description="This name appears on receipts and vouchers. You can change it later in Business settings."
          icon={<Building2 size={22} />}
        >
          <form
            className="grid gap-4"
            onSubmit={(e) =>
              void save(e, (d) =>
                api(
                  '/api/workspace/tenant',
                  'PUT',
                  {
                    name: text(d, 'name'),
                    timeZone: text(d, 'timeZone'),
                    overdueCreditDays: tenant.overdueCreditDays,
                    closingReminder: tenant.closingReminder.slice(0, 5),
                  },
                  user.tenantId,
                ),
              )
            }
          >
            <div className="form-grid">
              <Field
                label="Business name"
                name="name"
                defaultValue={tenant.name}
                required
                maxLength={120}
              />
              <Field
                label="Time zone"
                name="timeZone"
                defaultValue={tenant.timeZone}
                required
              />
            </div>
            <div className="setup-actions">
              {skip}
              <Button disabled={busy || !can('tenant.manage')}>
                Save and continue
              </Button>
            </div>
          </form>
        </StepCard>
      )}
      {step === 1 && (
        <StepCard
          title="Branch and store"
          description="Name your first branch and add the store or warehouse where you keep cylinders and gas."
          icon={<Building2 size={22} />}
        >
          <form
            className="grid gap-4"
            onSubmit={(e) =>
              void save(e, async (d) => {
                if (!branch)
                  throw new Error('Your account has no active branch.');
                if (text(d, 'branchName') !== branch.name)
                  await api(
                    `/api/workspace/branches/${branch.id}`,
                    'PUT',
                    {
                      name: text(d, 'branchName'),
                      active: true,
                      version: branch.version,
                    },
                    user.tenantId,
                  );
                await api(
                  '/api/workspace/inventory/warehouses',
                  'POST',
                  {
                    branchId: branch.id,
                    name: text(d, 'warehouse'),
                    location: text(d, 'location'),
                  },
                  user.tenantId,
                );
              })
            }
          >
            <div className="form-grid">
              <Field
                label="Branch name"
                name="branchName"
                defaultValue={branch?.name}
                required
                maxLength={120}
              />
              <Field
                label="Store or warehouse name"
                name="warehouse"
                defaultValue="Main store"
                required
                maxLength={120}
              />
              <Field
                label="Location (optional)"
                name="location"
                maxLength={200}
                placeholder="e.g. Back yard, rack A"
              />
            </div>
            <div className="setup-actions">
              {skip}
              <Button disabled={busy || !can('catalog.manage')}>
                Save and continue
              </Button>
            </div>
          </form>
        </StepCard>
      )}
      {step === 2 && (
        <StepCard
          title="Cash drawer"
          description="Count the cash you have now. This becomes the drawer used by sales, purchases and daily closing."
          icon={<Wallet size={22} />}
        >
          <form
            className="grid gap-4"
            onSubmit={(e) =>
              void save(e, (d) =>
                api(
                  '/api/workspace/finance/accounts',
                  'POST',
                  {
                    branchId: branch?.id,
                    name: text(d, 'name'),
                    kind: 'Cash',
                    opening: Number(text(d, 'opening')),
                    reason: 'Opening count during business setup',
                    idempotencyKey: crypto.randomUUID(),
                  },
                  user.tenantId,
                ),
              )
            }
          >
            <div className="form-grid">
              <Field
                label="Drawer name"
                name="name"
                defaultValue="Main drawer"
                required
                maxLength={120}
              />
              <Field
                label="Cash in the drawer now (PKR)"
                name="opening"
                type="number"
                min="0"
                step="0.01"
                defaultValue="0"
                required
              />
            </div>
            <div className="setup-actions">
              {skip}
              <Button disabled={busy || !can('finance.manage')}>
                Save and continue
              </Button>
            </div>
          </form>
        </StepCard>
      )}
      {step === 3 && (
        <StepCard
          title="Choose a plan"
          description="Keep using your free trial or request a plan now. Every paid plan is activated after the Gas365 team confirms your payment."
          icon={<Check size={22} />}
        >
          <PlanChooser user={user} />
          <div className="setup-actions">
            <Button type="button" onClick={next}>
              Continue
            </Button>
          </div>
        </StepCard>
      )}
      {step === 4 && (
        <section className="card setup-done" aria-labelledby="setup-done-title">
          <PartyPopper aria-hidden="true" size={40} />
          <h2 id="setup-done-title">Your business is ready</h2>
          <p className="section-help">
            Next, add your LPG, cylinder and spare-part items, record opening
            stock and add customers. The Getting started checklist on the
            overview tracks what is left.
          </p>
          <div className="setup-actions center">
            <Button variant="outline" asChild>
              <Link to="/workspace/inventory">Add items</Link>
            </Button>
            <Button onClick={() => navigate('/workspace')}>
              Go to overview
            </Button>
          </div>
        </section>
      )}
    </div>
  );
}
