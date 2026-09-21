import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { DataTable } from './components/DataTable';
import type { Page, Warehouse } from './inventory-types';

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
};
type Draft = {
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

export function SalesDraftPage({ user }: { user: Me }) {
  const branch = useBranch();
  return (
    <DraftWorkspace
      key={`${user.tenantId}:${branch}`}
      user={user}
      branch={branch}
    />
  );
}
function DraftWorkspace({ user, branch }: { user: Me; branch: string }) {
  const cache = useQueryClient();
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [editor, setEditor] = useState<Draft | 'new' | null>(null);
  const [detail, setDetail] = useState<Draft | null>(null);
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
  const canEdit =
    user.permissions.includes('sales.create') &&
    user.permissions.includes('customers.view');
  if (!branch) return <p>Select a branch to prepare sales drafts.</p>;
  return (
    <div className="grid gap-6">
      <p>
        Drafts are for review. They do not reserve stock or record payments.
      </p>
      {canEdit && (
        <Button onClick={() => setEditor('new')}>New sales draft</Button>
      )}
      {editor && (
        <DraftEditor
          key={editor === 'new' ? 'new' : `${editor.id}:${editor.version}`}
          user={user}
          branch={branch}
          initial={editor === 'new' ? null : editor}
          close={() => setEditor(null)}
          saved={async (d) => {
            setEditor(null);
            setDetail(d);
            await cache.invalidateQueries({ queryKey: ['sales-drafts'] });
          }}
        />
      )}
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
              <Button variant="ghost" onClick={() => setDetail(row.original)}>
                {row.original.reference}
              </Button>
            ),
          },
          {
            header: 'Customer',
            cell: ({ row }) => row.original.snapshot.customerName,
          },
          {
            header: 'Net',
            cell: ({ row }) => money(row.original.snapshot.net),
          },
          ...(canEdit
            ? [
                {
                  header: 'Actions',
                  cell: ({ row }: { row: { original: Draft } }) => (
                    <Button
                      variant="outline"
                      onClick={() => setEditor(row.original)}
                    >
                      Edit {row.original.reference}
                    </Button>
                  ),
                },
              ]
            : []),
        ]}
      />
      {detail && (
        <section className="panel grid gap-4" aria-label="Sales draft details">
          <h2>{detail.reference}</h2>
          <p>
            {detail.snapshot.customerName} — {detail.snapshot.priceListName}
          </p>
          {detail.snapshot.lines.map((line, i) => (
            <div key={i}>
              <strong>
                {line.name} ({line.mode})
              </strong>
              <p>
                {line.quantity} × {money(line.unitPrice)}; discount{' '}
                {money(line.discount)}; net {money(line.net)}
              </p>
            </div>
          ))}
          <dl>
            <dt>Gross</dt>
            <dd>{money(detail.snapshot.gross)}</dd>
            <dt>Discount</dt>
            <dd>{money(detail.snapshot.discount)}</dd>
            <dt>Net</dt>
            <dd>{money(detail.snapshot.net)}</dd>
          </dl>
          <p>{detail.snapshot.notes}</p>
          <Button variant="outline" onClick={() => setDetail(null)}>
            Close draft
          </Button>
        </section>
      )}
    </div>
  );
}
function Lookup({
  user,
  path,
  label,
  choose,
}: {
  user: Me;
  path: string;
  label: string;
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
  return (
    <div className="grid gap-2">
      <Field
        label={`Find ${label}`}
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
      />
      {rows.error && <p role="alert">{rows.error.message}</p>}
      {rows.isPending && <p role="status">Loading {label}...</p>}
      <div className="flex flex-wrap gap-2">
        {rows.data?.items
          .filter((p) => p.active)
          .map((p) => (
            <Button
              key={p.id}
              type="button"
              variant="outline"
              onClick={() => choose(p)}
            >
              {p.name}
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
          Previous {label}
        </Button>
        <Button
          type="button"
          variant="outline"
          disabled={page * 20 >= (rows.data?.total ?? 0)}
          onClick={() => setPage((p) => p + 1)}
        >
          Next {label}
        </Button>
      </div>
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
    <form
      aria-label="Sales draft editor"
      className="panel grid gap-4"
      onSubmit={save}
    >
      <h2>{initial ? 'Edit sales draft' : 'Prepare sales draft'}</h2>
      {error && <p role="alert">{error}</p>}
      <Field
        label="Sales reference"
        name="reference"
        required
        maxLength={120}
        defaultValue={initial?.reference ?? ''}
      />
      <Field
        label="Sales notes"
        name="notes"
        maxLength={300}
        defaultValue={initial?.snapshot.notes ?? ''}
      />
      <Lookup
        user={user}
        path="customers"
        label="customer"
        choose={setCustomer}
      />
      <p>Customer: {customer?.name ?? 'Choose a customer'}</p>
      <Lookup
        user={user}
        path="price-lists"
        label="price list"
        choose={(p) => {
          if (p.id !== list?.id) {
            setList(p);
            setLines([]);
            setPricePage(1);
          }
        }}
      />
      <p>
        Price list: {list?.name ?? 'Choose a price list'}. Changing the list
        clears the draft lines.
      </p>
      {prices.error && <p role="alert">{prices.error.message}</p>}
      {warehouses.error && <p role="alert">{warehouses.error.message}</p>}
      {list && (
        <div className="grid gap-2">
          <p>Add items at their saved selling price:</p>
          <div className="flex flex-wrap gap-2">
            {prices.data?.items
              .filter((x) => x.active)
              .map((p) => (
                <Button
                  key={p.id}
                  type="button"
                  variant="outline"
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
                      },
                    ])
                  }
                >
                  Add {p.sku} ({p.mode})
                </Button>
              ))}
          </div>
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
        </div>
      )}
      {lines.map((line, i) => (
        <fieldset key={line.key} className="panel grid gap-3">
          <legend>
            Line {i + 1}: {line.name} ({line.mode})
          </legend>
          <label htmlFor={`warehouse-${line.key}`}>
            Warehouse for line {i + 1}
          </label>
          <select
            id={`warehouse-${line.key}`}
            required
            value={line.warehouseId}
            onChange={(e) => change(line.key, { warehouseId: e.target.value })}
          >
            <option value="">Choose warehouse</option>
            {visibleWarehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
          <Field
            label={`Quantity for line ${i + 1}`}
            type="number"
            min="0.001"
            max="1000000"
            step="0.001"
            required
            value={line.quantity}
            onChange={(e) => change(line.key, { quantity: e.target.value })}
          />
          <Field
            label={`Discount for line ${i + 1} (PKR)`}
            type="number"
            min="0"
            step="0.01"
            required
            disabled={!discount}
            value={line.discount}
            onChange={(e) => change(line.key, { discount: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              setLines((old) => old.filter((x) => x.key !== line.key))
            }
          >
            Remove line {i + 1}
          </Button>
        </fieldset>
      ))}
      <p>
        Save to review totals calculated by the server. Changed selling prices
        require removing and adding the affected line with the latest price.
      </p>
      <div className="flex gap-3">
        <Button disabled={busy || !customer || !list || !lines.length}>
          Save sales draft
        </Button>
        <Button type="button" variant="outline" disabled={busy} onClick={close}>
          Cancel sales draft
        </Button>
      </div>
    </form>
  );
}
