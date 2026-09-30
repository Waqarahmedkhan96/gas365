import {
  useEffect,
  useId,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Banknote,
  CheckCircle2,
  Download,
  Eye,
  HandCoins,
  MapPin,
  Pencil,
  Phone,
  Plus,
  Receipt,
  RefreshCw,
  Store,
  Trash2,
  Truck,
  Wallet,
} from 'lucide-react';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { Modal } from './components/ui/feedback';
import { DataTable } from './components/DataTable';
import { PageActions } from './components/page-actions';
import {
  BackButton,
  Card,
  KeyValues,
  PageState,
  Pager,
  StatCard,
  StatGrid,
  StatusChip,
  Toolbar,
} from './components/page';
import { statusTone, useViewFocus } from './lib/ui-helpers';
import { businessDate, type Dashboard } from './report-types';
import type { Page, Item, Warehouse } from './inventory-types';
import type { Purchase, Supplier, SupplierProfile } from './purchasing-types';

const root = '/api/workspace/purchasing';
const money = (value: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
function Select({
  label,
  children,
  ...props
}: {
  label: string;
  children: ReactNode;
  name?: string;
  value?: string;
  onChange?: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  defaultValue?: string;
}) {
  const id = useId();
  return (
    <div className="grid gap-2">
      <label htmlFor={id}>{label}</label>
      <select id={id} required {...props}>
        {children}
      </select>
    </div>
  );
}
function form(event: FormEvent<HTMLFormElement>) {
  event.preventDefault();
  return new FormData(event.currentTarget);
}
const str = (data: FormData, key: string) => String(data.get(key) || '');
function initials(name: string) {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('') || '?'
  );
}

type View =
  | { kind: 'list' }
  | { kind: 'edit'; purchase?: Purchase }
  | { kind: 'purchase'; id: string; from?: View }
  | { kind: 'supplier'; id: string };

export function PurchasingPage({
  user,
  suppliersOnly,
  timeZone,
}: {
  user: Me;
  suppliersOnly: boolean;
  timeZone: string;
}) {
  const branch = useBranch();
  return (
    <PurchasingWorkspace
      key={`${user.tenantId}:${branch}:${suppliersOnly}`}
      user={user}
      branch={branch}
      suppliersOnly={suppliersOnly}
      timeZone={timeZone}
    />
  );
}
function PurchasingWorkspace({
  user,
  branch,
  suppliersOnly,
  timeZone,
}: {
  user: Me;
  branch: string;
  suppliersOnly: boolean;
  timeZone: string;
}) {
  const cache = useQueryClient();
  const has = (p: string) => user.permissions.includes(p);
  const [view, setView] = useState<View>({ kind: 'list' });
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [search, setSearch] = useState('');
  const [supplierEdit, setSupplierEdit] = useState<Supplier | 'new' | null>(
    null,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const keys = useRef(new Map<string, string>());
  const get = <T,>(path: string) =>
    api<T>(root + path, 'GET', undefined, user.tenantId);
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [view]);
  const suppliers = useQuery({
    queryKey: ['purchasing', user.tenantId, 'suppliers', search, pagination],
    queryFn: () =>
      get<Page<Supplier>>(
        `/suppliers?search=${encodeURIComponent(search)}&page=${pagination.pageIndex + 1}`,
      ),
  });
  const purchases = useQuery({
    queryKey: ['purchasing', user.tenantId, branch, pagination],
    enabled: !!branch && !suppliersOnly,
    queryFn: () =>
      get<Page<Purchase>>(
        `/purchases?branchId=${branch}&page=${pagination.pageIndex + 1}`,
      ),
  });
  const today = businessDate(timeZone);
  const figures = useQuery({
    queryKey: ['dashboard', user.tenantId, today, today, branch],
    enabled: !!branch && !suppliersOnly && has('reports.view'),
    queryFn: () =>
      api<Dashboard>(
        `/api/workspace/reports/dashboard?from=${today}&to=${today}&branchId=${branch}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  async function run(action: () => Promise<unknown>) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
      await cache.invalidateQueries({ queryKey: ['purchasing'] });
      await cache.invalidateQueries({ queryKey: ['inventory-items'] });
      await cache.invalidateQueries({ queryKey: ['dashboard'] });
      setNotice('Saved.');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function command(path: string, body: object) {
    const fingerprint = JSON.stringify([path, body]);
    let key = keys.current.get(fingerprint);
    if (!key) {
      key = crypto.randomUUID();
      keys.current.set(fingerprint, key);
    }
    const result = await api<Purchase>(
      root + path,
      'POST',
      { ...body, idempotencyKey: key },
      user.tenantId,
    );
    keys.current.delete(fingerprint);
    setView((v) =>
      v.kind === 'purchase' && v.id === result.id
        ? v
        : { kind: 'purchase', id: result.id, from: { kind: 'list' } },
    );
  }
  const open = (next: View) => {
    setError('');
    setNotice('');
    setView(next);
  };
  const feedback = (
    <>
      {error && (
        <p role="alert" className="error-banner">
          {error} For an uncertain response, retry without changing the command.
        </p>
      )}
      {notice && (
        <p role="status" className="notice-banner">
          <CheckCircle2 aria-hidden="true" size={18} /> {notice}
        </p>
      )}
    </>
  );
  const supplierDialog = (
    <Modal
      title={supplierEdit === 'new' ? 'Add supplier' : 'Edit supplier'}
      description="Supplier details appear on purchases and vouchers."
      open={!!supplierEdit}
      onOpenChange={(value) => {
        if (!value) setSupplierEdit(null);
      }}
    >
      {supplierEdit && (
        <form
          key={
            supplierEdit === 'new'
              ? 'new'
              : `${supplierEdit.id}:${supplierEdit.version}`
          }
          className="grid gap-4"
          aria-label="Supplier editor"
          onSubmit={(e) => {
            const d = form(e);
            void run(async () => {
              await api(
                root +
                  '/suppliers' +
                  (supplierEdit === 'new' ? '' : '/' + supplierEdit.id),
                supplierEdit === 'new' ? 'POST' : 'PUT',
                {
                  name: str(d, 'name'),
                  phone: str(d, 'phone'),
                  address: str(d, 'address'),
                  active: d.get('active') === 'on',
                  version: supplierEdit === 'new' ? 0 : supplierEdit.version,
                },
                user.tenantId,
              );
              setSupplierEdit(null);
            });
          }}
        >
          <Field
            label="Supplier name"
            name="name"
            required
            maxLength={120}
            defaultValue={supplierEdit === 'new' ? '' : supplierEdit.name}
          />
          <Field
            label="Phone"
            name="phone"
            maxLength={80}
            inputMode="tel"
            defaultValue={supplierEdit === 'new' ? '' : supplierEdit.phone}
          />
          <Field
            label="Address"
            name="address"
            maxLength={300}
            defaultValue={supplierEdit === 'new' ? '' : supplierEdit.address}
          />
          <label className="check">
            <input
              type="checkbox"
              name="active"
              defaultChecked={supplierEdit === 'new' || supplierEdit.active}
            />
            Active supplier
          </label>
          {error && <p role="alert">{error}</p>}
          <div className="dialog-actions">
            <Button
              type="button"
              variant="outline"
              onClick={() => setSupplierEdit(null)}
            >
              Cancel
            </Button>
            <Button disabled={busy}>Save supplier</Button>
          </div>
        </form>
      )}
    </Modal>
  );

  if (view.kind === 'edit')
    return (
      <>
        {feedback}
        <DraftEditor
          key={view.purchase ? view.purchase.id || 'replacement' : 'new'}
          initial={view.purchase}
          user={user}
          branch={branch}
          busy={busy}
          cancel={() =>
            open(
              view.purchase?.id
                ? { kind: 'purchase', id: view.purchase.id }
                : { kind: 'list' },
            )
          }
          save={(body, id) =>
            void run(async () => {
              const d = await api<Purchase>(
                root + '/purchases' + (id ? '/' + id : ''),
                id ? 'PUT' : 'POST',
                body,
                user.tenantId,
              );
              setView({ kind: 'purchase', id: d.id });
            })
          }
        />
      </>
    );
  if (view.kind === 'purchase')
    return (
      <>
        {feedback}
        <PurchaseDetail
          key={view.id}
          user={user}
          id={view.id}
          busy={busy}
          run={run}
          command={command}
          back={() => open(view.from ?? { kind: 'list' })}
          backLabel={
            view.from?.kind === 'supplier'
              ? 'Back to supplier'
              : suppliersOnly
                ? 'Back to suppliers'
                : 'Back to purchases'
          }
          edit={(p) => open({ kind: 'edit', purchase: p })}
        />
      </>
    );
  if (view.kind === 'supplier')
    return (
      <>
        {feedback}
        <SupplierPage
          user={user}
          branch={branch}
          id={view.id}
          canEdit={has('suppliers.manage')}
          back={() => open({ kind: 'list' })}
          edit={(s) => setSupplierEdit(s)}
          openPurchase={(id) => open({ kind: 'purchase', id, from: view })}
        />
        {supplierDialog}
      </>
    );

  if (suppliersOnly) {
    const total = suppliers.data?.total ?? 0;
    return (
      <div className="grid gap-6">
        {has('suppliers.manage') && (
          <PageActions>
            <Button onClick={() => setSupplierEdit('new')}>
              <Plus aria-hidden="true" size={18} /> Add supplier
            </Button>
          </PageActions>
        )}
        {feedback}
        {!branch && (
          <p role="status" className="muted-text">
            Choose a branch to see supplier balances.
          </p>
        )}
        <Toolbar
          search={{
            label: 'Search suppliers',
            placeholder: 'Search suppliers by name or phone',
            value: search,
            onChange: (text) => {
              setSearch(text);
              setPagination({ pageIndex: 0, pageSize: 20 });
            },
          }}
        >
          <span className="toolbar-count">{total} suppliers</span>
        </Toolbar>
        {supplierDialog}
        {suppliers.isPending ? (
          <PageState
            kind="loading"
            title="Loading suppliers"
            description="Please wait while suppliers are loaded."
          />
        ) : suppliers.error ? (
          <PageState
            kind="error"
            title="Suppliers unavailable"
            description={suppliers.error.message}
            retry={() => void suppliers.refetch()}
          />
        ) : !suppliers.data.items.length ? (
          <PageState
            kind="empty"
            title={search ? 'No matching suppliers' : 'No suppliers yet'}
            description={
              search
                ? 'Try another name or phone number.'
                : 'Add the companies and agents you buy gas, cylinders and parts from.'
            }
          />
        ) : (
          <section aria-label="Suppliers" className="grid gap-4">
            <ul className="entity-grid">
              {suppliers.data.items.map((row) => (
                <li key={row.id} className="entity-card">
                  <div className="entity-card-top">
                    <span className="avatar large" aria-hidden="true">
                      {initials(row.name)}
                    </span>
                    <div className="entity-card-title">
                      <button
                        type="button"
                        className="table-link"
                        onClick={() => open({ kind: 'supplier', id: row.id })}
                      >
                        {row.name}
                      </button>
                      <StatusChip tone={row.active ? 'success' : 'neutral'}>
                        {row.active ? 'Active' : 'Inactive'}
                      </StatusChip>
                    </div>
                  </div>
                  <ul className="entity-facts">
                    <li>
                      <Phone aria-hidden="true" size={16} />
                      {row.phone || 'No phone'}
                    </li>
                    <li>
                      <MapPin aria-hidden="true" size={16} />
                      {row.address || 'No address'}
                    </li>
                  </ul>
                  <div className="entity-actions">
                    <Button
                      variant="outline"
                      disabled={!branch}
                      onClick={() => open({ kind: 'supplier', id: row.id })}
                    >
                      <Eye aria-hidden="true" size={16} />
                      View<span className="sr-only"> {row.name} profile</span>
                    </Button>
                    {has('suppliers.manage') && (
                      <Button
                        variant="outline"
                        onClick={() => setSupplierEdit(row)}
                      >
                        <Pencil aria-hidden="true" size={16} />
                        Edit<span className="sr-only"> {row.name}</span>
                      </Button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
            <Pager
              page={pagination.pageIndex + 1}
              pageSize={pagination.pageSize}
              total={total}
              onChange={(page) =>
                setPagination((p) => ({ ...p, pageIndex: page - 1 }))
              }
            />
          </section>
        )}
      </div>
    );
  }

  if (!branch)
    return (
      <PageState
        kind="empty"
        title="Choose a branch"
        description="Select a branch in the top bar to work with purchases."
      />
    );
  const payables = figures.data?.metrics.find((m) => m.key === 'payables');
  return (
    <div className="grid gap-6">
      {has('purchases.create') && (
        <PageActions>
          <Button onClick={() => open({ kind: 'edit' })}>
            <Plus aria-hidden="true" size={18} /> New purchase
          </Button>
        </PageActions>
      )}
      {feedback}
      <StatGrid label="Purchasing summary">
        <StatCard
          label="Purchases"
          icon={Truck}
          value={purchases.data?.total ?? '—'}
          note="Drafts and posted, selected branch"
        />
        {payables && (
          <StatCard
            label="Supplier payables"
            icon={HandCoins}
            tone={payables.value > 0 ? 'warning' : 'neutral'}
            value={money(payables.value)}
            note="Owed to suppliers, selected branch"
          />
        )}
        <StatCard
          label="Suppliers"
          icon={Store}
          value={suppliers.data?.total ?? '—'}
          note="In your supplier directory"
        />
      </StatGrid>
      <DataTable
        title="Purchases"
        data={purchases.data?.items || []}
        rowCount={purchases.data?.total || 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        loading={purchases.isPending}
        error={purchases.error?.message}
        retry={() => void purchases.refetch()}
        columns={[
          {
            header: 'Reference',
            cell: ({ row }) => (
              <button
                type="button"
                className="table-link"
                onClick={() => open({ kind: 'purchase', id: row.original.id })}
              >
                {row.original.snapshot.reference}
              </button>
            ),
          },
          { header: 'Supplier', accessorKey: 'supplierName' },
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
            header: 'Outstanding',
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
    </div>
  );
}

function SupplierPage({
  user,
  branch,
  id,
  canEdit,
  back,
  edit,
  openPurchase,
}: {
  user: Me;
  branch: string;
  id: string;
  canEdit: boolean;
  back: () => void;
  edit: (supplier: Supplier) => void;
  openPurchase: (id: string) => void;
}) {
  const heading = useViewFocus<HTMLHeadingElement>();
  const [ledgerPage, setLedgerPage] = useState(1);
  const profile = useQuery({
    queryKey: ['purchasing', user.tenantId, branch, id, ledgerPage],
    enabled: !!branch,
    queryFn: () =>
      api<SupplierProfile>(
        `${root}/suppliers/${id}?branchId=${branch}&page=${ledgerPage}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const data = profile.data;
  return (
    <section className="detail-page" aria-label="Supplier profile">
      <BackButton onClick={back}>Back to suppliers</BackButton>
      {!branch && (
        <PageState
          kind="empty"
          title="Choose a branch"
          description="Supplier balances are kept per branch."
        />
      )}
      {profile.isPending && branch && (
        <PageState
          kind="loading"
          title="Loading supplier"
          description="Please wait while the supplier is loaded."
        />
      )}
      {profile.error && (
        <PageState
          kind="error"
          title="Supplier unavailable"
          description={profile.error.message}
          retry={() => void profile.refetch()}
        />
      )}
      {data && (
        <>
          <div className="profile-hero">
            <span className="avatar hero" aria-hidden="true">
              {initials(data.supplier.name)}
            </span>
            <div className="profile-hero-text">
              <h2 ref={heading} tabIndex={-1}>
                {data.supplier.name}{' '}
                <StatusChip tone={data.supplier.active ? 'success' : 'neutral'}>
                  {data.supplier.active ? 'Active' : 'Inactive'}
                </StatusChip>
              </h2>
              <ul className="entity-facts inline">
                <li>
                  <Phone aria-hidden="true" size={16} />
                  {data.supplier.phone || 'No phone'}
                </li>
                <li>
                  <MapPin aria-hidden="true" size={16} />
                  {data.supplier.address || 'No address'}
                </li>
              </ul>
            </div>
            {canEdit && (
              <div className="detail-actions">
                <Button onClick={() => edit(data.supplier)}>
                  <Pencil aria-hidden="true" size={16} /> Edit supplier
                </Button>
              </div>
            )}
          </div>
          <StatGrid label="Supplier balance">
            <StatCard
              label="Payable in selected branch"
              icon={HandCoins}
              tone={data.balance > 0 ? 'warning' : 'success'}
              value={money(data.balance)}
              note="Owed to this supplier"
            />
            <StatCard
              label="Unapplied supplier advances"
              icon={Wallet}
              value={money(data.unapplied)}
              note="Paid ahead, not yet allocated"
            />
          </StatGrid>
          <Card title="Supplier ledger">
            <p className="section-help">
              Invoice activity is listed here; finance permissions give access
              to the complete Udhaar history.
            </p>
            {!data.ledger.items.length ? (
              <p className="muted-text">No purchases in this branch yet.</p>
            ) : (
              <ol className="timeline ledger-timeline">
                {data.ledger.items.map(({ purchaseId, entry }) => (
                  <li key={entry.id}>
                    <div className="timeline-head">
                      <strong>{entry.kind}</strong>
                      <span>{entry.businessDate}</span>
                    </div>
                    <p>
                      Payable {money(entry.payable)} · Cash {money(entry.cash)}
                    </p>
                    {entry.reason && <p>{entry.reason}</p>}
                    <button
                      type="button"
                      className="table-link"
                      onClick={() => openPurchase(purchaseId)}
                    >
                      Open purchase {purchaseId.slice(0, 8)}
                    </button>
                  </li>
                ))}
              </ol>
            )}
            {data.ledger.total > 20 && (
              <div className="flex gap-2 pt-4">
                <Button
                  variant="outline"
                  disabled={ledgerPage === 1}
                  onClick={() => setLedgerPage((x) => x - 1)}
                >
                  Previous ledger page
                </Button>
                <Button
                  variant="outline"
                  disabled={ledgerPage * 20 >= data.ledger.total}
                  onClick={() => setLedgerPage((x) => x + 1)}
                >
                  Next ledger page
                </Button>
              </div>
            )}
          </Card>
        </>
      )}
    </section>
  );
}

function PurchaseDetail({
  user,
  id,
  busy,
  run,
  command,
  back,
  backLabel,
  edit,
}: {
  user: Me;
  id: string;
  busy: boolean;
  run: (action: () => Promise<unknown>) => Promise<void>;
  command: (path: string, body: object) => Promise<void>;
  back: () => void;
  backLabel: string;
  edit: (purchase: Purchase) => void;
}) {
  const has = (p: string) => user.permissions.includes(p);
  const focus = useViewFocus<HTMLDivElement>();
  const detail = useQuery({
    queryKey: ['purchasing', user.tenantId, 'detail', id],
    queryFn: () =>
      api<Purchase>(`${root}/purchases/${id}`, 'GET', undefined, user.tenantId),
  });
  const p = detail.data;
  return (
    <div className="detail-page" ref={focus} tabIndex={-1}>
      <BackButton onClick={back}>{backLabel}</BackButton>
      {detail.isPending && (
        <PageState
          kind="loading"
          title="Loading purchase"
          description="Please wait while the purchase is loaded."
        />
      )}
      {detail.error && (
        <PageState
          kind="error"
          title="Purchase unavailable"
          description={detail.error.message}
          retry={() => void detail.refetch()}
        />
      )}
      {p && (
        <section className="grid gap-6" aria-label="Purchase details">
          <div className="detail-header">
            <div>
              <h2>
                Purchase {p.snapshot.reference}{' '}
                <StatusChip tone={statusTone(p.status)}>{p.status}</StatusChip>
              </h2>
              <p>
                {p.supplierName}
                {p.correctsId ? ` · Corrects purchase ${p.correctsId}` : ''}
              </p>
            </div>
            <div className="detail-actions">
              <Button
                variant="outline"
                disabled={detail.isFetching}
                onClick={() => void detail.refetch()}
              >
                <RefreshCw aria-hidden="true" size={16} /> Refresh purchase
              </Button>
              {p.status === 'Draft' && has('purchases.create') && (
                <Button variant="outline" onClick={() => edit(p)}>
                  <Pencil aria-hidden="true" size={16} /> Edit draft
                </Button>
              )}
              {p.status !== 'Draft' && (
                <Button
                  variant="outline"
                  disabled={busy}
                  onClick={() =>
                    void run(async () => {
                      const response = await fetch(
                        `${root}/purchases/${p.id}/voucher`,
                        {
                          headers: { 'X-Workspace': user.tenantId },
                          credentials: 'same-origin',
                          cache: 'no-store',
                        },
                      );
                      if (!response.ok)
                        throw new Error('Voucher could not be generated.');
                      const url = URL.createObjectURL(await response.blob());
                      const a = document.createElement('a');
                      a.href = url;
                      a.download = `Gas365-purchase-${p.id}.pdf`;
                      a.click();
                      setTimeout(() => URL.revokeObjectURL(url), 10000);
                    })
                  }
                >
                  <Download aria-hidden="true" size={16} /> Download PDF voucher
                </Button>
              )}
            </div>
          </div>
          <StatGrid label="Purchase totals">
            <StatCard label="Total" icon={Receipt} value={money(p.total)} />
            <StatCard
              label="Paid to supplier"
              icon={Banknote}
              tone="success"
              value={money(p.paid)}
            />
            <StatCard
              label="Outstanding"
              icon={HandCoins}
              tone={p.outstanding > 0 ? 'warning' : 'success'}
              value={money(p.outstanding)}
            />
            <StatCard
              label="Landed charges"
              icon={Truck}
              value={money(p.snapshot.landedCharges)}
              note={`Allocated by ${p.snapshot.allocationRule.toLowerCase()}`}
            />
          </StatGrid>
          <div className="split-layout">
            <div className="grid gap-6 content-start">
              <Card title="Items received">
                <div
                  className="line-table"
                  role="region"
                  aria-label="Purchase lines"
                  tabIndex={0}
                >
                  <table>
                    <thead>
                      <tr>
                        <th scope="col">Item</th>
                        <th scope="col">Quantity</th>
                        <th scope="col">Base</th>
                        <th scope="col">Landed</th>
                        <th scope="col">Value</th>
                      </tr>
                    </thead>
                    <tbody>
                      {p.snapshot.lines.map((line, i) => (
                        <tr key={i}>
                          <td>
                            <strong>
                              {line.sku} · {line.name}
                            </strong>
                            <span className="cell-note">
                              {line.warehouse} · {line.bucket}
                            </span>
                            <span className="cell-note">
                              Receipt unit cost: {line.receiptUnitCost} PKR
                            </span>
                            {line.serials.length > 0 && (
                              <span className="cell-note">
                                Serials: {line.serials.join(', ')}
                              </span>
                            )}
                          </td>
                          <td>
                            {line.quantity} {line.unit}
                          </td>
                          <td>{money(line.baseValue)}</td>
                          <td>{money(line.landedCost)}</td>
                          <td>
                            <strong>{money(line.value)}</strong>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {p.snapshot.notes && (
                  <p className="muted-text pt-4">{p.snapshot.notes}</p>
                )}
              </Card>
              {p.status !== 'Draft' && (
                <Card title="Payment and correction history">
                  <ol className="timeline">
                    {p.entries.map((entry) => (
                      <li key={entry.id}>
                        <div className="timeline-head">
                          <strong>{entry.kind}</strong>
                          <span>{entry.businessDate}</span>
                        </div>
                        <p>
                          Payable {money(entry.payable)} · Cash{' '}
                          {money(entry.cash)}
                        </p>
                        {entry.reason && <p>{entry.reason}</p>}
                        {(entry.kind === 'Payment' ||
                          entry.kind === 'CostPayment') &&
                          p.status !== 'Reversed' &&
                          !p.entries.some((x) => x.reversesId === entry.id) &&
                          has('purchases.reverse') &&
                          has('purchases.pay') && (
                            <ReasonForm
                              label="Reverse payment"
                              busy={busy}
                              action={(reason) =>
                                void run(() =>
                                  command(
                                    `/purchases/${p.id}/payments/${entry.id}/reverse`,
                                    { reason },
                                  ),
                                )
                              }
                            />
                          )}
                      </li>
                    ))}
                  </ol>
                </Card>
              )}
            </div>
            <aside className="grid gap-6 content-start">
              {p.status === 'Draft' && has('purchases.post') && (
                <Card title="Post purchase" className="accent-card">
                  <form
                    className="grid gap-3"
                    onSubmit={(e) => {
                      const d = form(e);
                      void run(() =>
                        command(`/purchases/${p.id}/post`, {
                          version: p.version,
                          cashPaid: str(d, 'cash') || '0',
                        }),
                      );
                    }}
                  >
                    <p className="section-help">
                      Posting receives the stock at the server-calculated cost.
                      Corrections require a reversal.
                    </p>
                    <Field
                      label="Cash paid on posting (PKR)"
                      name="cash"
                      defaultValue="0"
                      inputMode="decimal"
                      disabled={!has('purchases.pay')}
                    />
                    <Field
                      label="Confirm permanent posting"
                      type="checkbox"
                      required
                    />
                    <Button disabled={busy} className="w-full">
                      Post purchase
                    </Button>
                  </form>
                </Card>
              )}
              {p.status !== 'Draft' &&
                p.status !== 'Reversed' &&
                p.outstanding > 0 &&
                has('purchases.pay') && (
                  <Card title="Record cash payment" className="accent-card">
                    <form
                      className="grid gap-3"
                      onSubmit={(e) => {
                        const d = form(e);
                        void run(() =>
                          command(`/purchases/${p.id}/payments`, {
                            amount: str(d, 'amount'),
                            reason: str(d, 'reason'),
                            expectedLastEntryId: p.entries.at(-1)!.id,
                          }),
                        );
                      }}
                    >
                      <Field
                        label="Payment amount (PKR)"
                        name="amount"
                        required
                        inputMode="decimal"
                      />
                      <Field
                        label="Payment reference or reason"
                        name="reason"
                        required
                        maxLength={300}
                      />
                      <Button disabled={busy} className="w-full">
                        Record payment
                      </Button>
                    </form>
                  </Card>
                )}
              {p.status !== 'Draft' && p.separateCosts && (
                <Card title="Separately paid purchase costs">
                  <p className="section-help">
                    Petrol, tolls, loading and unloading increase stock cost.
                    They are paid separately from the goods supplier.
                  </p>
                  <KeyValues
                    className="pt-3"
                    items={[
                      ['Costs paid', money(p.costsPaid)],
                      ['Costs unpaid', money(p.costsOutstanding)],
                    ]}
                  />
                  {p.status !== 'Reversed' &&
                    p.costsOutstanding > 0 &&
                    has('purchases.pay') && (
                      <form
                        className="grid gap-3 pt-4"
                        onSubmit={(e) => {
                          const d = form(e);
                          void run(() =>
                            command(`/purchases/${p.id}/cost-payments`, {
                              amount: str(d, 'amount'),
                              reason: str(d, 'reason'),
                              expectedLastEntryId: p.entries.at(-1)!.id,
                            }),
                          );
                        }}
                      >
                        <Field
                          label="Purchase cost payment (PKR)"
                          name="amount"
                          required
                          inputMode="decimal"
                        />
                        <Field
                          label="Cost type, recipient and reference"
                          name="reason"
                          required
                          maxLength={300}
                        />
                        <Button disabled={busy} className="w-full">
                          Record cost payment
                        </Button>
                      </form>
                    )}
                </Card>
              )}
              {p.status !== 'Draft' &&
                p.status !== 'Reversed' &&
                has('purchases.reverse') && (
                  <Card title="Reverse purchase" className="danger-card">
                    <p className="section-help">
                      Reverse recorded payments first. Later stock activity can
                      prevent reversal.
                    </p>
                    <ReasonForm
                      label="Reverse purchase"
                      busy={busy}
                      action={(reason) =>
                        void run(() =>
                          command(`/purchases/${p.id}/reverse`, { reason }),
                        )
                      }
                    />
                  </Card>
                )}
              {p.status === 'Reversed' && has('purchases.create') && (
                <Card title="Correct this purchase">
                  <p className="section-help">
                    Create a replacement draft that references this reversed
                    purchase.
                  </p>
                  <Button
                    className="w-full"
                    onClick={() =>
                      edit({ ...p, id: '', version: 0, correctsId: p.id })
                    }
                  >
                    Create replacement draft
                  </Button>
                </Card>
              )}
            </aside>
          </div>
        </section>
      )}
    </div>
  );
}
function ReasonForm({
  label,
  busy,
  action,
}: {
  label: string;
  busy: boolean;
  action: (reason: string) => void;
}) {
  return (
    <form
      className="inline-form"
      onSubmit={(e) => action(str(form(e), 'reason'))}
    >
      <Field label={`${label} reason`} name="reason" required maxLength={300} />
      <Field
        label={`Confirm ${label.toLowerCase()}`}
        type="checkbox"
        required
      />
      <Button variant="destructive" disabled={busy}>
        {label}
      </Button>
    </form>
  );
}
type EditLine = {
  itemId: string;
  warehouseId: string;
  bucket: string;
  quantity: string;
  unitCost: string;
  weightKg: string;
  serials: string;
};
const blank = (): EditLine => ({
  itemId: '',
  warehouseId: '',
  bucket: 'Available',
  quantity: '1',
  unitCost: '0',
  weightKg: '0',
  serials: '',
});
function DraftEditor({
  initial,
  user,
  branch,
  busy,
  save,
  cancel,
}: {
  initial?: Purchase;
  user: Me;
  branch: string;
  busy: boolean;
  save: (body: object, id?: string) => void;
  cancel: () => void;
}) {
  const heading = useViewFocus<HTMLHeadingElement>();
  const [lines, setLines] = useState<EditLine[]>(
    initial
      ? initial.snapshot.lines.map((x) => ({
          itemId: x.itemId,
          warehouseId: x.warehouseId,
          bucket: x.bucket,
          quantity: String(x.quantity),
          unitCost: String(x.price),
          weightKg: String(x.weightKg),
          serials: x.serials.join(', '),
        }))
      : [blank()],
  );
  const [itemSearch, setItemSearch] = useState('');
  const [supplierSearch, setSupplierSearch] = useState('');
  const [supplier, setSupplier] = useState(initial?.supplierId || '');
  const items = useQuery({
    queryKey: ['purchase-items', user.tenantId, itemSearch],
    queryFn: () =>
      api<Page<Item>>(
        `/api/workspace/inventory/items?search=${encodeURIComponent(itemSearch)}&pageSize=100`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const suppliers = useQuery({
    queryKey: ['purchase-supplier-options', user.tenantId, supplierSearch],
    queryFn: () =>
      api<Page<Supplier>>(
        `${root}/suppliers?search=${encodeURIComponent(supplierSearch)}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const warehouses = useQuery({
    queryKey: ['purchase-warehouses', user.tenantId],
    queryFn: () =>
      api<Warehouse[]>(
        '/api/workspace/inventory/warehouses',
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  function update(i: number, value: Partial<EditLine>) {
    setLines((old) =>
      old.map((line, j) => (i === j ? { ...line, ...value } : line)),
    );
  }
  const supplierName =
    suppliers.data?.items.find((s) => s.id === supplier)?.name ||
    (initial?.supplierId === supplier ? initial?.supplierName : '') ||
    'Choose a supplier';
  const itemName = (id: string) =>
    items.data?.items.find((x) => x.id === id)?.name ||
    initial?.snapshot.lines.find((x) => x.itemId === id)?.name ||
    'Choose an item';
  const replacement = !!initial && !initial.id;
  return (
    <div className="detail-page">
      <BackButton onClick={cancel}>
        {initial?.id ? 'Back to purchase' : 'Back to purchases'}
      </BackButton>
      <div className="detail-header">
        <div>
          <h2 ref={heading} tabIndex={-1}>
            {initial?.id
              ? 'Edit purchase draft'
              : replacement
                ? 'Replacement purchase draft'
                : 'New purchase draft'}
          </h2>
          <p>
            Record goods received from a supplier. Save the draft to see
            server-calculated amounts, then post it.
          </p>
        </div>
      </div>
      <form
        className="split-layout"
        aria-label="Purchase draft"
        onSubmit={(e) => {
          const d = form(e);
          save(
            {
              branchId: branch,
              supplierId: supplier,
              reference: str(d, 'reference'),
              notes: str(d, 'notes'),
              allocationRule: str(d, 'allocation'),
              landedCharges: str(d, 'charges'),
              version: initial?.version || 0,
              correctsId: initial?.correctsId || null,
              lines: lines.map((l) => ({
                ...l,
                serials: l.serials
                  .split(',')
                  .map((s) => s.trim())
                  .filter(Boolean),
              })),
            },
            initial?.id || undefined,
          );
        }}
      >
        <div className="grid gap-6">
          {[items.error, suppliers.error, warehouses.error]
            .filter(Boolean)
            .map((e, i) => (
              <p key={i} role="alert">
                {e!.message}
              </p>
            ))}
          <Card title="Supplier">
            <div className="form-grid">
              <Field
                label="Find supplier"
                value={supplierSearch}
                placeholder="Search by supplier name"
                onChange={(e) => setSupplierSearch(e.target.value)}
              />
              <Select
                label="Supplier"
                value={supplier}
                onChange={(e) => setSupplier(e.target.value)}
              >
                <option value="">Choose supplier</option>
                {initial &&
                  !suppliers.data?.items.some(
                    (s) => s.id === initial.supplierId,
                  ) && (
                    <option value={initial.supplierId}>
                      {initial.supplierName}
                    </option>
                  )}
                {suppliers.data?.items
                  .filter((s) => s.active)
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
              </Select>
            </div>
          </Card>
          <Card title="Purchase details">
            <div className="form-grid">
              <Field
                label="Supplier reference"
                name="reference"
                required
                maxLength={120}
                placeholder="Supplier invoice or challan number"
                defaultValue={initial?.snapshot.reference}
              />
              <Field
                label="Purchase notes"
                name="notes"
                maxLength={300}
                defaultValue={initial?.snapshot.notes}
              />
              <Select
                label="Landed-cost allocation"
                name="allocation"
                defaultValue={initial?.snapshot.allocationRule || 'Value'}
              >
                {['Value', 'Quantity', 'Weight'].map((x) => (
                  <option key={x}>{x}</option>
                ))}
              </Select>
              <Field
                label="Transport/loading charges (PKR)"
                name="charges"
                required
                inputMode="decimal"
                defaultValue={initial?.snapshot.landedCharges || '0'}
              />
            </div>
            <p className="section-help pt-3">
              Charges increase stock cost and are paid separately from the goods
              supplier. Weight uses each line&apos;s total received kilograms.
            </p>
          </Card>
          <Card title={`Items (${lines.length})`}>
            <div className="grid gap-4">
              <Field
                label="Find catalog item"
                value={itemSearch}
                placeholder="Filter the item lists below by SKU or name"
                onChange={(e) => setItemSearch(e.target.value)}
              />
              {lines.map((line, i) => (
                <fieldset key={i} className="line-card">
                  <legend>Line {i + 1}</legend>
                  <div className="form-grid">
                    <Select
                      label={`Item ${i + 1}`}
                      value={line.itemId}
                      onChange={(e) => {
                        const item = items.data?.items.find(
                          (x) => x.id === e.target.value,
                        );
                        update(i, {
                          itemId: e.target.value,
                          bucket:
                            item?.kind === 'Cylinder' ? 'Empty' : 'Available',
                        });
                      }}
                    >
                      <option value="">Choose item</option>
                      {line.itemId &&
                        !items.data?.items.some(
                          (x) => x.id === line.itemId,
                        ) && (
                          <option value={line.itemId}>
                            {initial?.snapshot.lines.find(
                              (x) => x.itemId === line.itemId,
                            )?.name || line.itemId}
                          </option>
                        )}
                      {items.data?.items
                        .filter((x) => x.active)
                        .map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.sku} - {x.name}
                          </option>
                        ))}
                    </Select>
                    <Select
                      label={`Warehouse ${i + 1}`}
                      value={line.warehouseId}
                      onChange={(e) =>
                        update(i, { warehouseId: e.target.value })
                      }
                    >
                      <option value="">Choose warehouse</option>
                      {warehouses.data
                        ?.filter((x) => x.branchId === branch && x.active)
                        .map((x) => (
                          <option key={x.id} value={x.id}>
                            {x.name}
                          </option>
                        ))}
                    </Select>
                    <Select
                      label={`State ${i + 1}`}
                      value={line.bucket}
                      onChange={(e) => update(i, { bucket: e.target.value })}
                    >
                      {[
                        'Available',
                        'Full',
                        'Empty',
                        'InTransit',
                        'CustomerHeld',
                        'Inspection',
                        'Damaged',
                        'Lost',
                        'Retired',
                      ].map((x) => (
                        <option key={x}>{x}</option>
                      ))}
                    </Select>
                    <Field
                      label={`Quantity ${i + 1}`}
                      required
                      inputMode="decimal"
                      value={line.quantity}
                      onChange={(e) => update(i, { quantity: e.target.value })}
                    />
                    <Field
                      label={`Unit price ${i + 1} (PKR)`}
                      required
                      inputMode="decimal"
                      value={line.unitCost}
                      onChange={(e) => update(i, { unitCost: e.target.value })}
                    />
                    <Field
                      label={`Total weight ${i + 1} (kg)`}
                      required
                      inputMode="decimal"
                      value={line.weightKg}
                      onChange={(e) => update(i, { weightKg: e.target.value })}
                    />
                    <Field
                      label={`Serials ${i + 1} (comma separated)`}
                      value={line.serials}
                      onChange={(e) => update(i, { serials: e.target.value })}
                    />
                  </div>
                  <div>
                    <Button
                      type="button"
                      variant="ghost"
                      className="danger-text"
                      disabled={lines.length === 1}
                      onClick={() =>
                        setLines((old) => old.filter((_, j) => i !== j))
                      }
                    >
                      <Trash2 aria-hidden="true" size={16} />
                      Remove line {i + 1}
                    </Button>
                  </div>
                </fieldset>
              ))}
              <div>
                <Button
                  type="button"
                  variant="outline"
                  disabled={lines.length >= 100}
                  onClick={() => setLines((old) => [...old, blank()])}
                >
                  <Plus aria-hidden="true" size={16} /> Add line
                </Button>
              </div>
            </div>
          </Card>
        </div>
        <aside className="sticky-aside">
          <Card title="Draft summary" className="accent-card">
            <KeyValues
              items={[
                ['Supplier', supplierName],
                ['Lines', lines.length],
              ]}
            />
            <ul className="summary-lines">
              {lines.map((line, i) => (
                <li key={i}>
                  <span>
                    {i + 1}. {line.itemId ? itemName(line.itemId) : 'No item'}
                  </span>
                  <span>× {line.quantity || 0}</span>
                </li>
              ))}
            </ul>
            <p className="section-help">
              Values, landed costs and receipt unit costs are calculated by the
              server when you save.
            </p>
            <div className="grid gap-2">
              <Button
                className="w-full"
                disabled={
                  busy ||
                  items.isPending ||
                  suppliers.isPending ||
                  warehouses.isPending
                }
              >
                Save draft
              </Button>
              <Button
                type="button"
                variant="outline"
                className="w-full"
                onClick={cancel}
              >
                Cancel draft editing
              </Button>
            </div>
          </Card>
        </aside>
      </form>
    </div>
  );
}
