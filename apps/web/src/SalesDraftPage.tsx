import { useEffect, useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  ClipboardList,
  FileClock,
  HandCoins,
  Pencil,
  Plus,
  ShoppingCart,
  Trash2,
  TrendingUp,
} from 'lucide-react';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { DataTable } from './components/DataTable';
import { PageActions } from './components/page-actions';
import {
  BackButton,
  Card,
  KeyValues,
  PageState,
  StatCard,
  StatGrid,
  StatusChip,
  Tabs,
} from './components/page';
import { useViewFocus } from './lib/ui-helpers';
import type { Page, Warehouse } from './inventory-types';
import { businessDate, type Dashboard } from './report-types';
import { SalesRegister, PostSale, SaleDetailPage } from './SalesRegister';
import { CylinderWorkspace } from './CylinderWorkspace';

type Party = { id: string; name: string; active: boolean };
type Price = {
  id: string;
  itemId: string;
  sku: string;
  itemName: string;
  unit: string;
  mode: string;
  unitPrice: number;
  version: number;
  active: boolean;
};
type Line = {
  priceId: string;
  priceVersion: number;
  itemId: string;
  sku: string;
  name: string;
  mode: string;
  unit: string;
  warehouseId: string;
  warehouse: string;
  quantity: number;
  unitPrice: number;
  gross: number;
  discount: number;
  net: number;
  cylinderReference: string;
  sourceCylinderReference: string;
  lossQuantity: number;
  lossReason: string;
  gasQuantity: number;
  suggestedDeposit: number;
  usageTerms: string;
};
export type Draft = {
  id: string;
  branchId: string;
  customerId: string;
  priceListId: string;
  reference: string;
  version: number;
  snapshot: {
    customerName: string;
    priceListName: string;
    notes: string;
    lines: Line[];
    gross: number;
    discount: number;
    net: number;
  };
};
type InputLine = {
  key: string;
  priceId: string;
  priceVersion: number;
  name: string;
  mode: string;
  warehouseId: string;
  quantity: string;
  discount: string;
  cylinderReference: string;
  sourceCylinderReference: string;
  lossQuantity: string;
  lossReason: string;
};
type View =
  | { kind: 'list' }
  | { kind: 'edit'; draft: Draft | null }
  | { kind: 'draft'; draft: Draft }
  | { kind: 'sale'; id: string };
const root = '/api/workspace/sales';
const modeLabels: Record<string, string> = {
  Refill: 'Gas / refill',
  FullCylinder: 'Full cylinder',
  CylinderOnly: 'Cylinder only',
  Exchange: 'Exchange',
  LoanRefill: 'Borrowed cylinder',
  SparePart: 'Spare part',
};
const money = (n: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);

export function SalesDraftPage({
  user,
  timeZone,
}: {
  user: Me;
  timeZone: string;
}) {
  const branch = useBranch();
  return (
    <DraftWorkspace
      key={`${user.tenantId}:${branch}`}
      user={user}
      branch={branch}
      timeZone={timeZone}
    />
  );
}

function SalesSummary({
  user,
  branch,
  timeZone,
  drafts,
}: {
  user: Me;
  branch: string;
  timeZone: string;
  drafts: number | undefined;
}) {
  const today = businessDate(timeZone);
  const reports = user.permissions.includes('reports.view');
  const figures = useQuery({
    queryKey: ['dashboard', user.tenantId, today, today, branch],
    enabled: reports,
    queryFn: () =>
      api<Dashboard>(
        `/api/workspace/reports/dashboard?from=${today}&to=${today}&branchId=${branch}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const posted = useQuery({
    queryKey: ['posted-sales', user.tenantId, branch, 'summary'],
    queryFn: () =>
      api<Page<unknown>>(
        `${root}/posted?branchId=${branch}&page=1`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const metric = (key: string) =>
    figures.data?.metrics.find((m) => m.key === key);
  const revenue = metric('revenue');
  const receivables = metric('receivables');
  return (
    <StatGrid label="Sales summary">
      {revenue && (
        <StatCard
          label="Net revenue today"
          icon={TrendingUp}
          value={money(revenue.value)}
          note="Selected branch, posted sales"
        />
      )}
      {receivables && (
        <StatCard
          label="Customer receivables"
          icon={HandCoins}
          tone={receivables.value > 0 ? 'warning' : 'neutral'}
          value={money(receivables.value)}
          note="Outstanding customer balances"
        />
      )}
      <StatCard
        label="Posted sales"
        icon={ShoppingCart}
        value={posted.data?.total ?? '—'}
        note="All time, selected branch"
      />
      <StatCard
        label="Drafts awaiting review"
        icon={FileClock}
        tone={drafts ? 'warning' : 'neutral'}
        value={drafts ?? '—'}
        note="Drafts do not reserve stock"
      />
    </StatGrid>
  );
}

function DraftWorkspace({
  user,
  branch,
  timeZone,
}: {
  user: Me;
  branch: string;
  timeZone: string;
}) {
  const cache = useQueryClient();
  const [params, setParams] = useSearchParams();
  const canEdit =
    user.permissions.includes('sales.create') &&
    user.permissions.includes('customers.view');
  const [view, setView] = useState<View>(() =>
    params.get('new') === '1' && canEdit
      ? { kind: 'edit', draft: null }
      : { kind: 'list' },
  );
  const [tab, setTab] = useState('posted');
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  useEffect(() => {
    if (params.has('new')) setParams({}, { replace: true });
  }, [params, setParams]);
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [view]);
  const list = useQuery({
    queryKey: ['sales-drafts', user.tenantId, branch, pagination],
    enabled: !!branch,
    queryFn: () =>
      api<Page<Draft>>(
        `${root}/drafts?branchId=${branch}&page=${pagination.pageIndex + 1}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  if (!branch)
    return (
      <PageState
        kind="empty"
        title="Choose a branch"
        description="Select a branch in the top bar to prepare and review sales."
      />
    );
  if (view.kind === 'edit')
    return (
      <DraftEditor
        key={view.draft ? `${view.draft.id}:${view.draft.version}` : 'new'}
        user={user}
        branch={branch}
        initial={view.draft}
        close={() =>
          setView(
            view.draft
              ? { kind: 'draft', draft: view.draft }
              : { kind: 'list' },
          )
        }
        saved={async (d) => {
          setView({ kind: 'draft', draft: d });
          await cache.invalidateQueries({ queryKey: ['sales-drafts'] });
        }}
      />
    );
  if (view.kind === 'draft')
    return (
      <DraftDetail
        user={user}
        draft={view.draft}
        canEdit={canEdit}
        back={() => {
          setTab('drafts');
          setView({ kind: 'list' });
        }}
        edit={() => setView({ kind: 'edit', draft: view.draft })}
        posted={async () => {
          setTab('posted');
          setView({ kind: 'list' });
          await cache.invalidateQueries({ queryKey: ['sales-drafts'] });
          await cache.invalidateQueries({ queryKey: ['posted-sales'] });
          await cache.invalidateQueries({ queryKey: ['cylinder-loans'] });
          await cache.invalidateQueries({ queryKey: ['dashboard'] });
        }}
      />
    );
  if (view.kind === 'sale')
    return (
      <SaleDetailPage
        user={user}
        id={view.id}
        back={() => {
          setTab('posted');
          setView({ kind: 'list' });
        }}
      />
    );
  return (
    <div className="grid gap-6">
      {canEdit && (
        <PageActions>
          <Button onClick={() => setView({ kind: 'edit', draft: null })}>
            <Plus aria-hidden="true" size={18} /> New sales draft
          </Button>
        </PageActions>
      )}
      <SalesSummary
        user={user}
        branch={branch}
        timeZone={timeZone}
        drafts={list.data?.total}
      />
      <Tabs
        label="Sales sections"
        value={tab}
        onChange={setTab}
        tabs={[
          {
            id: 'posted',
            label: 'Posted sales',
            content: (
              <SalesRegister
                user={user}
                branch={branch}
                onOpen={(id) => setView({ kind: 'sale', id })}
              />
            ),
          },
          {
            id: 'drafts',
            label: 'Drafts',
            count: list.data?.total,
            content: (
              <section className="grid gap-3" aria-label="Sales drafts">
                <p className="section-help">
                  Drafts are for review. They do not reserve stock or record
                  payments until they are posted.
                </p>
                <DataTable
                  title="Sales drafts"
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
                            setView({ kind: 'draft', draft: row.original })
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
                      header: 'Lines',
                      cell: ({ row }) => row.original.snapshot.lines.length,
                    },
                    {
                      header: 'Net',
                      cell: ({ row }) => money(row.original.snapshot.net),
                    },
                    {
                      header: 'Status',
                      cell: () => <StatusChip tone="warning">Draft</StatusChip>,
                    },
                    ...(canEdit
                      ? [
                          {
                            header: 'Actions',
                            cell: ({ row }: { row: { original: Draft } }) => (
                              <Button
                                variant="outline"
                                onClick={() =>
                                  setView({ kind: 'edit', draft: row.original })
                                }
                              >
                                <Pencil aria-hidden="true" size={16} />
                                Edit
                                <span className="sr-only">
                                  {' '}
                                  {row.original.reference}
                                </span>
                              </Button>
                            ),
                          },
                        ]
                      : []),
                  ]}
                />
              </section>
            ),
          },
          {
            id: 'cylinders',
            label: 'Borrowed cylinders',
            content: <CylinderWorkspace user={user} branch={branch} />,
          },
        ]}
      />
    </div>
  );
}

function LinesTable({ lines }: { lines: Line[] }) {
  return (
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
            <th scope="col">Unit price</th>
            <th scope="col">Discount</th>
            <th scope="col">Net</th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line, i) => (
            <tr key={i}>
              <td>
                <strong>{line.name}</strong>
                <span className="cell-note">
                  {modeLabels[line.mode] ?? line.mode}
                  {line.warehouse ? ` · ${line.warehouse}` : ''}
                </span>
                {line.mode === 'Refill' && (
                  <span className="cell-note loss-note">
                    Cylinder {line.cylinderReference || `line ${i + 1}`}: Loss{' '}
                    {line.lossQuantity ?? 0} {line.unit}
                    {line.lossReason ? ` — ${line.lossReason}` : ''}
                  </span>
                )}
                {line.sourceCylinderReference && (
                  <span className="cell-note">
                    Source cylinder: {line.sourceCylinderReference}
                  </span>
                )}
              </td>
              <td>
                {line.quantity} {line.unit}
              </td>
              <td>{money(line.unitPrice)}</td>
              <td>{money(line.discount)}</td>
              <td>
                <strong>{money(line.net)}</strong>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function DraftDetail({
  user,
  draft,
  canEdit,
  back,
  edit,
  posted,
}: {
  user: Me;
  draft: Draft;
  canEdit: boolean;
  back: () => void;
  edit: () => void;
  posted: () => Promise<void>;
}) {
  const heading = useViewFocus<HTMLHeadingElement>();
  return (
    <section className="detail-page" aria-label="Sales draft details">
      <BackButton onClick={back}>Back to sales</BackButton>
      <div className="detail-header">
        <div>
          <h2 ref={heading} tabIndex={-1}>
            {draft.reference} <StatusChip tone="warning">Draft</StatusChip>
          </h2>
          <p>
            {draft.snapshot.customerName} — {draft.snapshot.priceListName}
          </p>
        </div>
        {canEdit && (
          <div className="detail-actions">
            <Button variant="outline" onClick={edit}>
              <Pencil aria-hidden="true" size={16} />
              Edit<span className="sr-only"> {draft.reference}</span>
            </Button>
          </div>
        )}
      </div>
      <StatGrid label="Draft totals">
        <StatCard label="Gross" value={money(draft.snapshot.gross)} />
        <StatCard label="Discount" value={money(draft.snapshot.discount)} />
        <StatCard
          label="Net"
          icon={ClipboardList}
          value={money(draft.snapshot.net)}
          note="Calculated by the server"
        />
      </StatGrid>
      <div className="split-layout">
        <div className="grid gap-6">
          <Card title="Items">
            <LinesTable lines={draft.snapshot.lines} />
          </Card>
          {draft.snapshot.notes && (
            <Card title="Notes">
              <p className="muted-text">{draft.snapshot.notes}</p>
            </Card>
          )}
        </div>
        <aside className="grid gap-6 content-start">
          {user.permissions.includes('sales.post') ? (
            <Card className="accent-card">
              <PostSale user={user} draft={draft} done={posted} />
            </Card>
          ) : (
            <Card title="Posting">
              <p className="muted-text">
                Ask a colleague with posting permission to review and post this
                draft.
              </p>
            </Card>
          )}
        </aside>
      </div>
    </section>
  );
}

function Lookup({
  user,
  path,
  label,
  selected,
  choose,
}: {
  user: Me;
  path: string;
  label: string;
  selected: string | undefined;
  choose: (party: Party) => void;
}) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const rows = useQuery({
    queryKey: ['sale-lookup', user.tenantId, path, search, page],
    queryFn: () =>
      api<Page<Party>>(
        `${root}/${path}?search=${encodeURIComponent(search)}&page=${page}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const total = rows.data?.total ?? 0;
  return (
    <div className="grid gap-3">
      <Field
        label={`Find ${label}`}
        value={search}
        placeholder={`Search by ${label} name`}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
      />
      {rows.error && <p role="alert">{rows.error.message}</p>}
      {rows.isPending && <p role="status">Loading {label}...</p>}
      <div className="option-grid">
        {rows.data?.items
          .filter((p) => p.active)
          .map((p) => (
            <Button
              key={p.id}
              type="button"
              variant="outline"
              className="option-button"
              aria-pressed={selected === p.id}
              onClick={() => choose(p)}
            >
              {selected === p.id && (
                <CheckCircle2 aria-hidden="true" size={16} />
              )}
              {p.name}
            </Button>
          ))}
      </div>
      {total > 20 && (
        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Previous {label}
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={page * 20 >= total}
            onClick={() => setPage((p) => p + 1)}
          >
            Next {label}
          </Button>
        </div>
      )}
    </div>
  );
}

function DraftEditor({
  user,
  branch,
  initial,
  close,
  saved,
}: {
  user: Me;
  branch: string;
  initial: Draft | null;
  close: () => void;
  saved: (draft: Draft) => Promise<void>;
}) {
  const heading = useViewFocus<HTMLHeadingElement>();
  const [customer, setCustomer] = useState<Party | null>(
    initial
      ? {
          id: initial.customerId,
          name: initial.snapshot.customerName,
          active: true,
        }
      : null,
  );
  const [list, setList] = useState<Party | null>(
    initial
      ? {
          id: initial.priceListId,
          name: initial.snapshot.priceListName,
          active: true,
        }
      : null,
  );
  const [lines, setLines] = useState<InputLine[]>(
    initial?.snapshot.lines.map((x) => ({
      key: crypto.randomUUID(),
      priceId: x.priceId,
      priceVersion: x.priceVersion,
      name: x.name,
      mode: x.mode,
      warehouseId: x.warehouseId,
      quantity: String(x.quantity),
      discount: String(x.discount),
      cylinderReference: x.cylinderReference ?? '',
      sourceCylinderReference: x.sourceCylinderReference ?? '',
      lossQuantity: String(x.lossQuantity ?? 0),
      lossReason: x.lossReason ?? '',
    })) ?? [],
  );
  const [pricePage, setPricePage] = useState(1);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const prices = useQuery({
    queryKey: ['draft-prices', user.tenantId, list?.id, pricePage],
    enabled: !!list,
    queryFn: () =>
      api<Page<Price>>(
        `${root}/price-lists/${list!.id}/prices?page=${pricePage}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const warehouses = useQuery({
    queryKey: ['draft-warehouses', user.tenantId, branch],
    queryFn: () =>
      api<Warehouse[]>(
        '/api/workspace/inventory/warehouses',
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const visibleWarehouses =
    warehouses.data?.filter((w) => w.branchId === branch && w.active) ?? [];
  const discount = user.permissions.includes('sales.discount');
  function change(key: string, patch: Partial<InputLine>) {
    setLines((old) => old.map((x) => (x.key === key ? { ...x, ...patch } : x)));
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError('');
    try {
      const d = await api<Draft>(
        `${root}/drafts${initial ? `/${initial.id}` : ''}`,
        initial ? 'PUT' : 'POST',
        {
          branchId: branch,
          customerId: customer?.id,
          priceListId: list?.id,
          reference: String(data.get('reference')),
          notes: String(data.get('notes')),
          version: initial?.version ?? 0,
          lines: lines.map((x) => ({
            priceId: x.priceId,
            priceVersion: x.priceVersion,
            warehouseId: x.warehouseId,
            quantity: x.quantity,
            discount: x.discount,
            cylinderReference: x.cylinderReference,
            sourceCylinderReference: x.sourceCylinderReference,
            lossQuantity: x.lossQuantity,
            lossReason: x.lossReason,
          })),
        },
        user.tenantId,
      );
      await saved(d);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="detail-page">
      <BackButton onClick={close}>
        {initial ? 'Back to draft' : 'Back to sales'}
      </BackButton>
      <div className="detail-header">
        <div>
          <h2 ref={heading} tabIndex={-1}>
            {initial ? `Edit ${initial.reference}` : 'New sales draft'}
          </h2>
          <p>
            Choose the customer and price list, then add items at their saved
            selling price.
          </p>
        </div>
      </div>
      <form
        aria-label="Sales draft editor"
        className="split-layout"
        onSubmit={save}
      >
        <div className="grid gap-6">
          <Card title="Sale details">
            <div className="form-grid">
              <Field
                label="Sales reference"
                name="reference"
                required
                maxLength={120}
                placeholder="e.g. INV-0001"
                defaultValue={initial?.reference ?? ''}
              />
              <Field
                label="Sales notes"
                name="notes"
                maxLength={300}
                defaultValue={initial?.snapshot.notes ?? ''}
              />
            </div>
          </Card>
          <Card title="Customer">
            <Lookup
              user={user}
              path="customers"
              label="customer"
              selected={customer?.id}
              choose={setCustomer}
            />
          </Card>
          <Card title="Price list and items">
            <div className="grid gap-4">
              <Lookup
                user={user}
                path="price-lists"
                label="price list"
                selected={list?.id}
                choose={(p) => {
                  if (p.id !== list?.id) {
                    setList(p);
                    setLines([]);
                    setPricePage(1);
                  }
                }}
              />
              <p className="section-help">
                Changing the price list clears the draft lines.
              </p>
              {prices.error && <p role="alert">{prices.error.message}</p>}
              {warehouses.error && (
                <p role="alert">{warehouses.error.message}</p>
              )}
              {list && (
                <div className="grid gap-3">
                  <h4 className="subheading">Add items</h4>
                  <div className="option-grid">
                    {prices.data?.items
                      .filter((x) => x.active)
                      .map((p) => (
                        <Button
                          key={p.id}
                          type="button"
                          variant="outline"
                          className="option-button"
                          disabled={lines.length >= 100}
                          onClick={() =>
                            setLines((old) => [
                              ...old,
                              {
                                key: crypto.randomUUID(),
                                priceId: p.id,
                                priceVersion: p.version,
                                name: p.itemName,
                                mode: p.mode,
                                warehouseId: visibleWarehouses[0]?.id ?? '',
                                quantity: '1',
                                discount: '0',
                                cylinderReference: '',
                                sourceCylinderReference: '',
                                lossQuantity: '0',
                                lossReason: '',
                              },
                            ])
                          }
                        >
                          <Plus aria-hidden="true" size={16} />
                          Add {p.sku} ({p.mode})
                        </Button>
                      ))}
                  </div>
                  {(prices.data?.total ?? 0) > 20 && (
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        disabled={pricePage === 1}
                        onClick={() => setPricePage((p) => p - 1)}
                      >
                        Previous prices
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        disabled={pricePage * 20 >= (prices.data?.total ?? 0)}
                        onClick={() => setPricePage((p) => p + 1)}
                      >
                        Next prices
                      </Button>
                    </div>
                  )}
                </div>
              )}
            </div>
          </Card>
          <Card title={`Lines (${lines.length})`}>
            {!lines.length && (
              <p className="muted-text">
                No items yet. Choose a price list and add items above.
              </p>
            )}
            <div className="grid gap-4">
              {lines.map((line, i) => (
                <fieldset key={line.key} className="line-card">
                  <legend>
                    Line {i + 1}: {line.name} ({line.mode})
                  </legend>
                  <div className="form-grid">
                    <div className="grid gap-2">
                      <label htmlFor={`warehouse-${line.key}`}>
                        Warehouse for line {i + 1}
                      </label>
                      <select
                        id={`warehouse-${line.key}`}
                        required
                        value={line.warehouseId}
                        onChange={(e) =>
                          change(line.key, { warehouseId: e.target.value })
                        }
                      >
                        <option value="">Choose warehouse</option>
                        {visibleWarehouses.map((w) => (
                          <option key={w.id} value={w.id}>
                            {w.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <Field
                      label={`Quantity for line ${i + 1}`}
                      type="number"
                      min="0.001"
                      max="1000000"
                      step="0.001"
                      required
                      value={line.quantity}
                      onChange={(e) =>
                        change(line.key, { quantity: e.target.value })
                      }
                    />
                    <Field
                      label={`Discount for line ${i + 1} (PKR)`}
                      type="number"
                      min="0"
                      step="0.01"
                      required
                      disabled={!discount}
                      value={line.discount}
                      onChange={(e) =>
                        change(line.key, { discount: e.target.value })
                      }
                    />
                  </div>
                  {line.mode === 'Refill' && (
                    <div className="grid gap-3">
                      <p className="section-help">
                        Use one line per cylinder filled. With a kg price, enter
                        0.1 for 100 g, 1.6 for 1,600 g or 3.5 for 3,500 g.
                      </p>
                      <div className="form-grid">
                        <Field
                          label={`Cylinder reference for line ${i + 1}`}
                          maxLength={120}
                          value={line.cylinderReference}
                          onChange={(e) =>
                            change(line.key, {
                              cylinderReference: e.target.value,
                            })
                          }
                        />
                        <Field
                          label={`Source cylinder reference for line ${i + 1}`}
                          maxLength={120}
                          value={line.sourceCylinderReference}
                          onChange={(e) =>
                            change(line.key, {
                              sourceCylinderReference: e.target.value,
                            })
                          }
                        />
                        <Field
                          label={`Loss for line ${i + 1} (same unit as quantity)`}
                          type="number"
                          min="0"
                          max="1000000"
                          step="0.001"
                          required
                          value={line.lossQuantity}
                          onChange={(e) =>
                            change(line.key, { lossQuantity: e.target.value })
                          }
                        />
                        <Field
                          label={`Loss reason for line ${i + 1}`}
                          maxLength={300}
                          value={line.lossReason}
                          onChange={(e) =>
                            change(line.key, { lossReason: e.target.value })
                          }
                        />
                      </div>
                      <p className="section-help">
                        Loss is deducted from stock without charging the
                        customer for it.
                      </p>
                    </div>
                  )}
                  <div>
                    <Button
                      type="button"
                      variant="ghost"
                      className="danger-text"
                      onClick={() =>
                        setLines((old) => old.filter((x) => x.key !== line.key))
                      }
                    >
                      <Trash2 aria-hidden="true" size={16} />
                      Remove line {i + 1}
                    </Button>
                  </div>
                </fieldset>
              ))}
            </div>
          </Card>
        </div>
        <aside className="sticky-aside">
          <Card title="Draft summary" className="accent-card">
            <KeyValues
              items={[
                ['Customer', customer?.name ?? 'Choose a customer'],
                ['Price list', list?.name ?? 'Choose a price list'],
                ['Lines', lines.length],
              ]}
            />
            {lines.length > 0 && (
              <ul className="summary-lines">
                {lines.map((line, i) => (
                  <li key={line.key}>
                    <span>
                      {i + 1}. {line.name}
                    </span>
                    <span>× {line.quantity || 0}</span>
                  </li>
                ))}
              </ul>
            )}
            <p className="section-help">
              Totals are calculated by the server when you save. Changed selling
              prices require removing and adding the affected line.
            </p>
            {error && <p role="alert">{error}</p>}
            <div className="grid gap-2">
              <Button
                className="w-full"
                disabled={busy || !customer || !list || !lines.length}
              >
                Save sales draft
              </Button>
              <Button
                type="button"
                variant="outline"
                className="w-full"
                disabled={busy}
                onClick={close}
              >
                Cancel sales draft
              </Button>
            </div>
          </Card>
        </aside>
      </form>
    </div>
  );
}
