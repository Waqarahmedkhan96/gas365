import { useRef, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { DataTable } from './components/DataTable';
import type { Page } from './inventory-types';
import type { Draft } from './SalesDraftPage';
import {
  BackButton,
  Card,
  KeyValues,
  PageState,
  StatCard,
  StatGrid,
  StatusChip,
} from './components/page';
import { statusTone, useViewFocus } from './lib/ui-helpers';
import {
  Banknote,
  Download,
  HandCoins,
  Receipt,
  TrendingUp,
} from 'lucide-react';

type Entry = {
  id: string;
  kind: string;
  cash: number;
  receivable: number;
  revenue: number;
  reason: string;
  businessDate: string;
  reversesId: string | null;
};
type Sale = {
  id: string;
  reference: string;
  status: string;
  total: number;
  paid: number;
  outstanding: number;
  cogs: number | null;
  grossProfit: number | null;
  snapshot: Draft['snapshot'];
  entries: Entry[];
  depositCollected: number;
  loanIds: string[];
};
const root = '/api/workspace/sales';
const money = (n: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);

// Keep the same key when retrying an unchanged payload after an uncertain response.
function useCommandKey() {
  const prior = useRef({ payload: '', key: '' });
  return (body: object) => {
    const payload = JSON.stringify(body);
    if (prior.current.payload !== payload)
      prior.current = { payload, key: crypto.randomUUID() };
    return prior.current.key;
  };
}

export function PostSale({
  user,
  draft,
  done,
}: {
  user: Me;
  draft: Draft;
  done: () => Promise<void>;
}) {
  const key = useCommandKey();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function post(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const body = {
      version: draft.version,
      cashPaid: String(data.get('cashPaid') ?? '0'),
      serials: draft.snapshot.lines.map((_, i) =>
        String(data.get(`serials-${i}`) ?? '')
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean),
      ),
      cylinders: draft.snapshot.lines.map((line, i) => ({
        incomingSerials: String(data.get(`incoming-${i}`) ?? '')
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean),
        depositPerCylinder:
          line.mode === 'LoanRefill'
            ? String(data.get(`deposit-${i}`) ?? '0')
            : '0',
      })),
    };
    setBusy(true);
    setError('');
    try {
      await api<Sale>(
        `${root}/drafts/${draft.id}/post`,
        'POST',
        { ...body, idempotencyKey: key(body) },
        user.tenantId,
      );
      await done();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="grid gap-3" aria-label="Post sale" onSubmit={post}>
      <h3 className="card-title">Post reviewed sale</h3>
      <p className="section-help">
        Posting records stock usage, filling loss and the customer balance.
        Corrections require a reversal.
      </p>
      {draft.snapshot.lines.map(
        (line, i) =>
          ['CylinderOnly', 'FullCylinder', 'Exchange', 'LoanRefill'].includes(
            line.mode,
          ) && (
            <Field
              key={i}
              label={`Serials for line ${i + 1} (comma separated, tracked cylinders only)`}
              name={`serials-${i}`}
            />
          ),
      )}
      {draft.snapshot.lines.map((line, i) => (
        <div key={`cylinder-${i}`}>
          {line.mode === 'Exchange' && (
            <Field
              label={`Incoming empty cylinder serials for line ${i + 1}`}
              name={`incoming-${i}`}
            />
          )}
          {line.mode === 'LoanRefill' && (
            <div className="grid gap-2">
              <p>
                Line {i + 1}: {line.quantity} borrowed cylinders,{' '}
                {line.gasQuantity} kg LPG each. {line.usageTerms}
              </p>
              <Field
                label={`Deposit collected per cylinder for line ${i + 1} (PKR)`}
                name={`deposit-${i}`}
                type="number"
                min="0"
                max="1000000000"
                step="0.01"
                required
                defaultValue={line.suggestedDeposit}
              />
              <p>
                Enter the amount actually collected. Zero or a reduced deposit
                needs authorization. Deposit cash is separate from the sale
                payment.
              </p>
            </div>
          )}
        </div>
      ))}
      {user.permissions.includes('sales.pay') && (
        <Field
          label="Cash received on sale (PKR)"
          name="cashPaid"
          type="number"
          min="0"
          step="0.01"
          defaultValue="0"
          required
        />
      )}
      {error && <p role="alert">{error}</p>}
      <Button disabled={busy} className="w-full">
        Post sale
      </Button>
    </form>
  );
}

export function SalesRegister({
  user,
  branch,
  customerId,
  onOpen,
}: {
  user: Me;
  branch: string;
  customerId?: string;
  onOpen?: (id: string) => void;
}) {
  const cache = useQueryClient();
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [selected, setSelected] = useState<string | null>(null);
  const list = useQuery({
    queryKey: ['posted-sales', user.tenantId, branch, customerId, pagination],
    queryFn: () =>
      api<Page<Sale>>(
        `${root}/posted?branchId=${branch}${customerId ? `&customerId=${customerId}` : ''}&page=${pagination.pageIndex + 1}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const detail = useQuery({
    queryKey: ['posted-sales', user.tenantId, selected],
    enabled: !!selected && !onOpen,
    queryFn: () =>
      api<Sale>(`${root}/posted/${selected}`, 'GET', undefined, user.tenantId),
  });
  return (
    <section className="grid gap-4" aria-label="Sales register">
      <DataTable
        title="Posted sales"
        data={list.data?.items ?? []}
        rowCount={list.data?.total ?? 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        loading={list.isPending}
        error={list.error?.message}
        retry={() => void list.refetch()}
        columns={[
          {
            header: 'Reference',
            cell: ({ row }) => (
              <button
                type="button"
                className="table-link"
                onClick={() =>
                  onOpen
                    ? onOpen(row.original.id)
                    : setSelected(row.original.id)
                }
              >
                {row.original.reference}
              </button>
            ),
          },
          {
            header: 'Customer',
            cell: ({ row }) => row.original.snapshot.customerName,
          },
          {
            header: 'Status',
            cell: ({ row }) => (
              <StatusChip tone={statusTone(row.original.status)}>
                {row.original.status}
              </StatusChip>
            ),
          },
          { header: 'Total', cell: ({ row }) => money(row.original.total) },
          { header: 'Paid', cell: ({ row }) => money(row.original.paid) },
          {
            header: 'Balance',
            cell: ({ row }) => (
              <span
                className={
                  row.original.outstanding > 0 ? 'amount-due' : undefined
                }
              >
                {money(row.original.outstanding)}
              </span>
            ),
          },
        ]}
      />
      {!onOpen && detail.error && <p role="alert">{detail.error.message}</p>}
      {!onOpen && detail.data && (
        <SaleDetail
          key={detail.data.id}
          sale={detail.data}
          user={user}
          refresh={async () => {
            await cache.invalidateQueries({ queryKey: ['posted-sales'] });
          }}
        />
      )}
    </section>
  );
}

// Full-page view of one posted sale, opened from the sales list.
export function SaleDetailPage({
  user,
  id,
  back,
}: {
  user: Me;
  id: string;
  back: () => void;
}) {
  const cache = useQueryClient();
  const heading = useViewFocus<HTMLDivElement>();
  const detail = useQuery({
    queryKey: ['posted-sales', user.tenantId, id],
    queryFn: () =>
      api<Sale>(`${root}/posted/${id}`, 'GET', undefined, user.tenantId),
  });
  return (
    <div className="detail-page" ref={heading} tabIndex={-1}>
      <BackButton onClick={back}>Back to sales</BackButton>
      {detail.isPending && (
        <PageState
          kind="loading"
          title="Loading sale"
          description="Please wait while the sale is loaded."
        />
      )}
      {detail.error && (
        <PageState
          kind="error"
          title="Sale unavailable"
          description={detail.error.message}
          retry={() => void detail.refetch()}
        />
      )}
      {detail.data && (
        <SaleDetail
          key={detail.data.id}
          sale={detail.data}
          user={user}
          refresh={async () => {
            await cache.invalidateQueries({ queryKey: ['posted-sales'] });
            await cache.invalidateQueries({ queryKey: ['dashboard'] });
          }}
        />
      )}
    </div>
  );
}

function SaleDetail({
  sale,
  user,
  refresh,
}: {
  sale: Sale;
  user: Me;
  refresh: () => Promise<void>;
}) {
  const key = useCommandKey();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function command(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const action = String(data.get('action'));
    const path = `${root}/posted/${sale.id}/${action}`;
    const body =
      action === 'payments'
        ? {
            amount: String(data.get('amount')),
            reason: String(data.get('reason')),
            expectedLastEntryId: sale.entries.at(-1)!.id,
          }
        : { reason: String(data.get('reason')) };
    setBusy(true);
    setError('');
    try {
      await api<Sale>(
        path,
        'POST',
        { ...body, idempotencyKey: key({ path, ...body }) },
        user.tenantId,
      );
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function receipt() {
    setBusy(true);
    setError('');
    try {
      const response = await fetch(`${root}/posted/${sale.id}/receipt`, {
        headers: { 'X-Workspace': user.tenantId },
        credentials: 'same-origin',
        cache: 'no-store',
      });
      if (!response.ok) throw new Error('Receipt could not be generated.');
      const url = URL.createObjectURL(await response.blob());
      const a = document.createElement('a');
      a.href = url;
      a.download = `Gas365-sale-${sale.id}.pdf`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 10000);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const canPay = user.permissions.includes('sales.pay');
  const canReverse = user.permissions.includes('sales.reverse');
  const open = sale.status !== 'Reversed';
  return (
    <section className="grid gap-6" aria-label="Posted sale details">
      <div className="detail-header">
        <div>
          <h2>
            {sale.reference}{' '}
            <StatusChip tone={statusTone(sale.status)}>
              {sale.status}
            </StatusChip>
          </h2>
          <p>
            {sale.snapshot.customerName} — {sale.snapshot.priceListName}
          </p>
        </div>
        <div className="detail-actions">
          <Button variant="outline" disabled={busy} onClick={receipt}>
            <Download aria-hidden="true" size={16} />
            Download sales receipt
          </Button>
        </div>
      </div>
      {error && <p role="alert">{error}</p>}
      <StatGrid label="Sale totals">
        <StatCard label="Total" icon={Receipt} value={money(sale.total)} />
        <StatCard
          label="Paid / allocated"
          icon={Banknote}
          tone="success"
          value={money(sale.paid)}
        />
        <StatCard
          label="Balance"
          icon={HandCoins}
          tone={sale.outstanding > 0 ? 'warning' : 'success'}
          value={money(sale.outstanding)}
          note={sale.outstanding > 0 ? 'Still owed by the customer' : 'Settled'}
        />
        {sale.cogs !== null && (
          <StatCard
            label="Gross profit"
            icon={TrendingUp}
            tone={sale.grossProfit! < 0 ? 'danger' : 'success'}
            value={money(sale.grossProfit!)}
            note={`Cost including filling loss ${money(sale.cogs)}`}
          />
        )}
      </StatGrid>
      <div className="split-layout">
        <div className="grid gap-6 content-start">
          <Card title="Items">
            <div
              className="line-table"
              role="region"
              aria-label="Sale lines"
              tabIndex={0}
            >
              <table>
                <thead>
                  <tr>
                    <th scope="col">Item</th>
                    <th scope="col">Quantity</th>
                    <th scope="col">Net</th>
                  </tr>
                </thead>
                <tbody>
                  {sale.snapshot.lines.map((line, i) => (
                    <tr key={i}>
                      <td>
                        <strong>
                          Line {i + 1}: {line.name}
                        </strong>
                        {line.sourceCylinderReference && (
                          <span className="cell-note">
                            Source cylinder: {line.sourceCylinderReference}
                          </span>
                        )}
                        {line.mode === 'Refill' && (
                          <span className="cell-note loss-note">
                            Cylinder {line.cylinderReference || `line ${i + 1}`}
                            : Loss {line.lossQuantity ?? 0} {line.unit} —{' '}
                            {line.lossReason}
                          </span>
                        )}
                      </td>
                      <td>
                        {line.quantity} {line.unit}
                      </td>
                      <td>
                        <strong>{money(line.net)}</strong>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
          <Card title="Payment and correction history">
            <ol className="timeline">
              {sale.entries.map((entry) => (
                <li key={entry.id}>
                  <div className="timeline-head">
                    <strong>{entry.kind}</strong>
                    <span>{entry.businessDate}</span>
                  </div>
                  <p>
                    Cash {money(entry.cash)} · balance movement{' '}
                    {money(entry.receivable)}
                    {entry.reason ? ` · ${entry.reason}` : ''}
                  </p>
                  {open &&
                    canPay &&
                    canReverse &&
                    entry.kind === 'Payment' &&
                    !sale.entries.some((e) => e.reversesId === entry.id) && (
                      <form className="inline-form" onSubmit={command}>
                        <input
                          type="hidden"
                          name="action"
                          value={`payments/${entry.id}/reverse`}
                        />
                        <Field
                          label={`Reason to reverse payment ${entry.id}`}
                          name="reason"
                          maxLength={300}
                          required
                        />
                        <Button variant="outline" disabled={busy}>
                          Reverse payment
                        </Button>
                      </form>
                    )}
                </li>
              ))}
            </ol>
          </Card>
        </div>
        <aside className="grid gap-6 content-start">
          {open && canPay && sale.outstanding > 0 && (
            <Card title="Receive payment" className="accent-card">
              <form
                className="grid gap-3"
                onSubmit={command}
                aria-label="Receive sales payment"
              >
                <input type="hidden" name="action" value="payments" />
                <Field
                  label="Payment amount (PKR)"
                  name="amount"
                  type="number"
                  min="0.01"
                  step="0.01"
                  required
                />
                <Field
                  label="Payment reason"
                  name="reason"
                  maxLength={300}
                  required
                />
                <Button disabled={busy} className="w-full">
                  Receive payment
                </Button>
              </form>
            </Card>
          )}
          <Card title="Summary">
            <KeyValues
              items={[
                ['Total', money(sale.total)],
                ['Paid / allocated', money(sale.paid)],
                ['Balance', money(sale.outstanding)],
                ...(sale.loanIds?.length > 0
                  ? ([
                      [
                        'Refundable deposits collected',
                        money(sale.depositCollected),
                      ],
                    ] as [string, string][])
                  : []),
                ...(sale.cogs !== null
                  ? ([
                      ['Cost including filling loss', money(sale.cogs)],
                      ['Gross profit', money(sale.grossProfit!)],
                    ] as [string, string][])
                  : []),
              ]}
            />
          </Card>
          {open && canReverse && (
            <Card title="Reverse sale" className="danger-card">
              <form
                className="grid gap-3"
                onSubmit={command}
                aria-label="Reverse sale"
              >
                <p className="section-help">
                  First reverse recorded sale payments. Reversing a
                  borrowed-cylinder sale also records the return of its deposit
                  cash and cylinder. Only proceed after handing that cash back.
                  Later stock activity can prevent reversal.
                </p>
                <input type="hidden" name="action" value="reverse" />
                <Field
                  label="Sale reversal reason"
                  name="reason"
                  maxLength={300}
                  required
                />
                <Button
                  variant="outline"
                  className="w-full"
                  disabled={busy || sale.paid !== 0}
                >
                  Reverse sale
                </Button>
              </form>
            </Card>
          )}
        </aside>
      </div>
    </section>
  );
}
