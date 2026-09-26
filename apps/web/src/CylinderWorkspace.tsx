import { useRef, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { DataTable } from './components/DataTable';
import type { Item, Page } from './inventory-types';

const root = '/api/workspace/sales';
const money = (n: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);
type Policy = {
  itemId: string;
  gasItemId: string;
  gasQuantity: number;
  suggestedDeposit: number;
  usageTerms: string;
  version: number;
};
type Entry = {
  id: string;
  kind: string;
  cash: number;
  liability: number;
  receivable: number;
  revenue: number;
  usageFee: number;
  damageOrLossCharge: number;
  condition: string;
  reason: string;
  reversesId: string | null;
};
type Loan = {
  id: string;
  saleId: string;
  customerId: string;
  serial: string;
  deposit: number;
  usageTerms: string;
  status: string;
  refundDue: number;
  amountToCollect: number;
  entries: Entry[];
};

function ItemChoice({
  user,
  kind,
  label,
  choose,
}: {
  user: Me;
  kind: string;
  label: string;
  choose: (item: Item) => void;
}) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const rows = useQuery({
    queryKey: ['cylinder-items', user.tenantId, kind, search, page],
    queryFn: () =>
      api<Page<Item>>(
        `/api/workspace/inventory/items?search=${encodeURIComponent(search)}&kind=${kind}&page=${page}&pageSize=20`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  return (
    <div className="grid gap-2">
      <Field
        label={label}
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
      />
      {rows.error && <p role="alert">{rows.error.message}</p>}
      <div className="flex flex-wrap gap-2">
        {rows.data?.items
          .filter((x) => x.kind === kind && x.active)
          .map((x) => (
            <Button
              type="button"
              variant="outline"
              key={x.id}
              onClick={() => choose(x)}
            >
              {x.sku} — {x.name}
            </Button>
          ))}
      </div>
      <div className="flex gap-2">
        <Button
          type="button"
          variant="outline"
          disabled={page === 1}
          onClick={() => setPage((p) => p - 1)}
        >
          Previous {kind}
        </Button>
        <Button
          type="button"
          variant="outline"
          disabled={page * 20 >= (rows.data?.total ?? 0)}
          onClick={() => setPage((p) => p + 1)}
        >
          Next {kind}
        </Button>
      </div>
    </div>
  );
}
export function CylinderWorkspace({
  user,
  branch,
  customerId,
}: {
  user: Me;
  branch: string;
  customerId?: string;
}) {
  const cache = useQueryClient();
  const [selected, setSelected] = useState<string | null>(null);
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const rows = useQuery({
    queryKey: ['cylinder-loans', user.tenantId, branch, customerId, pagination],
    queryFn: () =>
      api<Page<Loan>>(
        `${root}/loans?branchId=${branch}${customerId ? `&customerId=${customerId}` : ''}&page=${pagination.pageIndex + 1}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const detail = useQuery({
    queryKey: ['cylinder-loans', user.tenantId, selected],
    enabled: !!selected,
    queryFn: () =>
      api<Loan>(`${root}/loans/${selected}`, 'GET', undefined, user.tenantId),
  });
  return (
    <section className="grid gap-4" aria-label="Borrowed cylinders">
      {!customerId && user.permissions.includes('cylinders.configure') && (
        <PolicySetup user={user} />
      )}
      <DataTable
        title="Borrowed cylinders"
        data={rows.data?.items ?? []}
        rowCount={rows.data?.total ?? 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        loading={rows.isPending}
        error={rows.error?.message}
        retry={() => void rows.refetch()}
        columns={[
          {
            header: 'Loan reference',
            cell: ({ row }) => (
              <Button
                variant="ghost"
                onClick={() => setSelected(row.original.id)}
              >
                {row.original.serial || `Loan ${row.original.id.slice(0, 8)}`}
              </Button>
            ),
          },
          { header: 'Status', accessorKey: 'status' },
          { header: 'Deposit', cell: ({ row }) => money(row.original.deposit) },
          {
            header: 'Refund due',
            cell: ({ row }) => money(row.original.refundDue),
          },
          {
            header: 'To collect',
            cell: ({ row }) => money(row.original.amountToCollect),
          },
        ]}
      />
      {detail.error && <p role="alert">{detail.error.message}</p>}
      {detail.data && (
        <LoanDetail
          key={detail.data.id}
          user={user}
          loan={detail.data}
          refresh={async () => {
            await cache.invalidateQueries({ queryKey: ['cylinder-loans'] });
          }}
        />
      )}
    </section>
  );
}
function PolicySetup({ user }: { user: Me }) {
  const [item, setItem] = useState<Item | null>(null);
  const policy = useQuery({
    queryKey: ['cylinder-policy', user.tenantId, item?.id],
    enabled: !!item,
    queryFn: () =>
      api<Policy | null>(
        `${root}/cylinder-policies/${item!.id}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  return (
    <details className="panel">
      <summary>Configure cylinder LPG and deposit</summary>
      <p>
        Track shell stock separately from LPG. A filled cylinder uses the
        configured kilograms once when sold or exchanged. Shell opening/purchase
        cost must exclude LPG already recorded as gas stock.
      </p>
      <ItemChoice
        user={user}
        kind="Cylinder"
        label="Find cylinder type"
        choose={setItem}
      />
      {item && <p>Selected: {item.name}</p>}
      {policy.error && <p role="alert">{policy.error.message}</p>}
      {item && policy.isSuccess && (
        <PolicyEditor
          key={`${item.id}:${policy.data?.version ?? 0}`}
          user={user}
          item={item}
          initial={policy.data}
          saved={() => policy.refetch()}
        />
      )}
    </details>
  );
}
function PolicyEditor({
  user,
  item,
  initial,
  saved,
}: {
  user: Me;
  item: Item;
  initial: Policy | null;
  saved: () => unknown;
}) {
  const [gasId, setGasId] = useState(initial?.gasItemId ?? '');
  const [gasName, setGasName] = useState(
    initial?.gasItemId ?? 'Choose LPG item',
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const d = new FormData(e.currentTarget);
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await api<Policy>(
        `${root}/cylinder-policies/${item.id}`,
        'PUT',
        {
          gasItemId: gasId,
          gasQuantity: String(d.get('gas')),
          suggestedDeposit: String(d.get('deposit')),
          usageTerms: String(d.get('terms')),
          version: initial?.version ?? 0,
        },
        user.tenantId,
      );
      setNotice('Cylinder policy saved.');
      saved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="grid gap-3" aria-label="Cylinder policy" onSubmit={save}>
      <ItemChoice
        user={user}
        kind="Lpg"
        label="Find LPG item (KG unit)"
        choose={(x) => {
          setGasId(x.id);
          setGasName(x.name);
        }}
      />
      <p>LPG item: {gasName}</p>
      <Field
        label="LPG kg per filled cylinder"
        name="gas"
        type="number"
        min="0.001"
        max="1000000"
        step="0.001"
        required
        defaultValue={initial?.gasQuantity ?? item.capacityKg ?? ''}
      />
      <Field
        label="Suggested deposit per cylinder (PKR)"
        name="deposit"
        type="number"
        min="0"
        max="1000000000"
        step="0.01"
        required
        defaultValue={initial?.suggestedDeposit ?? 0}
      />
      <Field
        label="Usage fee terms shown to customer"
        name="terms"
        maxLength={300}
        required
        defaultValue={initial?.usageTerms ?? ''}
      />
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      <Button disabled={busy || !gasId}>Save cylinder policy</Button>
    </form>
  );
}
function LoanDetail({
  user,
  loan,
  refresh,
}: {
  user: Me;
  loan: Loan;
  refresh: () => Promise<void>;
}) {
  const prior = useRef({ body: '', key: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function command(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const d = new FormData(e.currentTarget);
    const action = String(d.get('action'));
    const body: Record<string, unknown> = { reason: String(d.get('reason')) };
    if (!action.endsWith('/reverse'))
      body.expectedLastEntryId = loan.entries.at(-1)!.id;
    if (action === 'return') {
      body.condition = String(d.get('condition'));
      body.usageFee = String(d.get('usage'));
      body.approvalId = String(d.get('approval') ?? '') || null;
    } else if (action === 'approvals') {
      body.condition = String(d.get('condition'));
      body.charge = String(d.get('charge'));
    } else if (action === 'payments' || action === 'refunds')
      body.amount = String(d.get('amount'));
    const signature = JSON.stringify({ id: loan.id, action, body });
    if (prior.current.body !== signature)
      prior.current = { body: signature, key: crypto.randomUUID() };
    setBusy(true);
    setError('');
    try {
      await api<Loan>(
        `${root}/loans/${loan.id}/${action}`,
        'POST',
        { ...body, idempotencyKey: prior.current.key },
        user.tenantId,
      );
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const approvals = loan.entries.filter(
    (x) =>
      x.kind === 'Approval' && !loan.entries.some((e) => e.reversesId === x.id),
  );
  return (
    <section className="panel grid gap-4" aria-label="Cylinder loan details">
      <h2 className="break-words">
        Cylinder {loan.serial || loan.id} — {loan.status}
      </h2>
      <p>{loan.usageTerms}</p>
      <dl>
        <dt>Deposit collected</dt>
        <dd>{money(loan.deposit)}</dd>
        <dt>Refund still owed</dt>
        <dd>{money(loan.refundDue)}</dd>
        <dt>Additional amount to collect</dt>
        <dd>{money(loan.amountToCollect)}</dd>
      </dl>
      {error && <p role="alert">{error}</p>}
      {loan.status === 'On loan' &&
        user.permissions.includes('cylinders.approve_damage_loss') && (
          <form
            className="grid gap-3"
            onSubmit={command}
            aria-label="Approve cylinder damage or loss"
          >
            <h3>Manager damage/loss approval</h3>
            <input type="hidden" name="action" value="approvals" />
            <label htmlFor={`approval-condition-${loan.id}`}>
              Approval condition
            </label>
            <select id={`approval-condition-${loan.id}`} name="condition">
              <option>Inspection</option>
              <option>Damaged</option>
              <option>Lost</option>
            </select>
            <Field
              label="Approved damage or loss charge (PKR)"
              name="charge"
              type="number"
              min="0"
              max="1000000000"
              step="0.01"
              required
              defaultValue="0"
            />
            <Field
              label="Approval reason"
              name="reason"
              maxLength={300}
              required
            />
            <Button disabled={busy}>Approve cylinder charge</Button>
          </form>
        )}
      {loan.status === 'On loan' &&
        user.permissions.includes('cylinders.return') && (
          <form
            className="grid gap-3"
            onSubmit={command}
            aria-label="Return borrowed cylinder"
          >
            <h3>Return or loss settlement</h3>
            <input type="hidden" name="action" value="return" />
            <label htmlFor={`return-condition-${loan.id}`}>
              Cylinder condition
            </label>
            <select id={`return-condition-${loan.id}`} name="condition">
              <option>Empty</option>
              <option>Inspection</option>
              <option>Damaged</option>
              <option>Lost</option>
            </select>
            <Field
              label="Usage fee for this return (PKR)"
              name="usage"
              type="number"
              min="0"
              max="1000000000"
              step="0.01"
              required
              defaultValue="0"
            />
            <label htmlFor={`approval-${loan.id}`}>
              Manager approval for damage or loss
            </label>
            <select id={`approval-${loan.id}`} name="approval">
              <option value="">No damage/loss charge</option>
              {approvals.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.condition} — {money(x.damageOrLossCharge)} — {x.reason}
                </option>
              ))}
            </select>
            <Field
              label="Return reason"
              name="reason"
              maxLength={300}
              required
            />
            <p>
              Lost cylinders remain marked Lost and do not return to available
              stock. Saving calculates the refund due; record cash separately
              after paying it.
            </p>
            <Button disabled={busy}>Record cylinder return</Button>
          </form>
        )}
      {user.permissions.includes('sales.pay') &&
        (loan.status === 'Returned' || loan.status === 'Lost') &&
        (['refunds', 'payments'] as const).map((action) => {
          const amount =
            action === 'refunds' ? loan.refundDue : loan.amountToCollect;
          if (amount <= 0) return null;
          const label = action === 'refunds' ? 'Refund' : 'Additional payment';
          return (
            <form key={action} className="grid gap-3" onSubmit={command}>
              <input type="hidden" name="action" value={action} />
              <Field
                label={`${label} amount (PKR)`}
                name="amount"
                type="number"
                min="0.01"
                step="0.01"
                required
                defaultValue={amount}
              />
              <Field
                label={`${label} reference`}
                name="reason"
                maxLength={300}
                required
              />
              <Button disabled={busy}>Record {label.toLowerCase()} cash</Button>
            </form>
          );
        })}
      <h3>Deposit and return history</h3>
      {loan.entries.map((e) => (
        <div key={e.id}>
          <p>
            {e.kind}: cash {money(e.cash)}, deposit liability{' '}
            {money(e.liability)}, usage fee {money(e.usageFee)}, damage/loss
            charge {money(e.damageOrLossCharge)} — {e.reason}
          </p>
          {['Return', 'Refund', 'Payment'].includes(e.kind) &&
            !loan.entries.some((x) => x.reversesId === e.id) &&
            user.permissions.includes('sales.reverse') &&
            (e.cash === 0 || user.permissions.includes('sales.pay')) && (
              <form className="grid gap-2" onSubmit={command}>
                <input
                  type="hidden"
                  name="action"
                  value={`entries/${e.id}/reverse`}
                />
                <Field
                  label={`Correction reason for ${e.kind} ${e.id}`}
                  name="reason"
                  maxLength={300}
                  required
                />
                <Button variant="outline" disabled={busy}>
                  Reverse cylinder {e.kind.toLowerCase()}
                </Button>
              </form>
            )}
        </div>
      ))}
    </section>
  );
}

type Arrangement = {
  customerId: string;
  zeroDepositApproved: boolean;
  reason: string;
  version: number;
};
export function DepositArrangement({
  user,
  customerId,
}: {
  user: Me;
  customerId: string;
}) {
  const row = useQuery({
    queryKey: ['deposit-arrangement', user.tenantId, customerId],
    queryFn: () =>
      api<Arrangement>(
        `${root}/customers/${customerId}/deposit-arrangement`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  return (
    <div className="grid gap-3">
      {row.error && <p role="alert">{row.error.message}</p>}
      {row.data && (
        <form
          key={row.data.version}
          className="grid gap-3"
          onSubmit={async (e) => {
            e.preventDefault();
            const d = new FormData(e.currentTarget);
            setBusy(true);
            setError('');
            try {
              await api<Arrangement>(
                `${root}/customers/${customerId}/deposit-arrangement`,
                'PUT',
                {
                  zeroDepositApproved: d.get('zero') === 'on',
                  reason: String(d.get('reason')),
                  version: row.data!.version,
                },
                user.tenantId,
              );
              await row.refetch();
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          <Field
            label="Approved zero-deposit customer"
            name="zero"
            type="checkbox"
            defaultChecked={row.data.zeroDepositApproved}
            disabled={!user.permissions.includes('cylinders.waive_deposit')}
          />
          <Field
            label="Deposit arrangement reason"
            name="reason"
            maxLength={300}
            required
            defaultValue={row.data.reason}
            disabled={!user.permissions.includes('cylinders.waive_deposit')}
          />
          {error && <p role="alert">{error}</p>}
          {user.permissions.includes('cylinders.waive_deposit') && (
            <Button disabled={busy}>Save deposit arrangement</Button>
          )}
        </form>
      )}
    </div>
  );
}
