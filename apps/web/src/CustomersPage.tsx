import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { DataTable } from './components/DataTable';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import type { Item, Page } from './inventory-types';

type Customer = {
  id: string;
  name: string;
  phone: string;
  address: string;
  active: boolean;
  version: number;
};
type PriceList = { id: string; name: string; active: boolean; version: number };
type Price = {
  id: string;
  itemId: string;
  sku: string;
  itemName: string;
  unit: string;
  mode: string;
  unitPrice: number;
  active: boolean;
  version: number;
};
const root = '/api/workspace/sales';
const modes: Record<string, string[]> = {
  Lpg: ['Refill'],
  Cylinder: ['FullCylinder', 'CylinderOnly', 'Exchange', 'LoanRefill'],
  SparePart: ['SparePart'],
};
const labels: Record<string, string> = {
  Refill: 'Gas / refill',
  FullCylinder: 'Full cylinder sale',
  CylinderOnly: 'Cylinder only',
  Exchange: 'Empty-for-full exchange',
  LoanRefill: 'Gas in a borrowed cylinder',
  SparePart: 'Spare part',
};
const money = (amount: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 6,
  }).format(amount);
const value = (data: FormData, name: string) => String(data.get(name) ?? '');

export function CustomersPage({
  user,
  pricesOnly,
}: {
  user: Me;
  pricesOnly: boolean;
}) {
  const cache = useQueryClient();
  const [search, setSearch] = useState('');
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [editor, setEditor] = useState<Customer | PriceList | 'new' | null>(
    null,
  );
  const [selected, setSelected] = useState<PriceList | null>(null);
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const canEdit = user.permissions.includes(
    pricesOnly ? 'prices.manage' : 'customers.manage',
  );
  const path = pricesOnly ? '/price-lists' : '/customers';
  const rows = useQuery({
    queryKey: ['sales-directory', user.tenantId, path, search, pagination],
    queryFn: () =>
      api<Page<Customer & PriceList>>(
        `${root}${path}?search=${encodeURIComponent(search)}&page=${pagination.pageIndex + 1}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const current = editor && editor !== 'new' ? editor : null;
      const saved = await api<Customer & PriceList>(
        root + path + (current ? `/${current.id}` : ''),
        current ? 'PUT' : 'POST',
        {
          name: value(data, 'name'),
          ...(!pricesOnly
            ? { phone: value(data, 'phone'), address: value(data, 'address') }
            : {}),
          active: data.has('active'),
          version: current?.version ?? 0,
        },
        user.tenantId,
      );
      if (pricesOnly && selected?.id === saved.id) setSelected(saved);
      if (!pricesOnly && customer?.id === saved.id) setCustomer(saved);
      setEditor(null);
      await cache.invalidateQueries({ queryKey: ['sales-directory'] });
      setNotice('Saved.');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-end gap-3">
        <Field
          label={pricesOnly ? 'Search price lists' : 'Search customers'}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPagination((p) => ({ ...p, pageIndex: 0 }));
          }}
        />
        {canEdit && (
          <Button
            onClick={() => {
              setEditor('new');
              setError('');
            }}
          >
            {pricesOnly ? 'Add price list' : 'Add customer'}
          </Button>
        )}
      </div>
      {notice && <p role="status">{notice}</p>}
      {error && <p role="alert">{error}</p>}
      {editor && (
        <form
          key={editor === 'new' ? 'new' : editor.id}
          aria-label={pricesOnly ? 'Price list editor' : 'Customer editor'}
          className="panel grid gap-4"
          onSubmit={save}
        >
          <h2>{pricesOnly ? 'Price list details' : 'Customer details'}</h2>
          <Field
            label={pricesOnly ? 'Price list name' : 'Customer name'}
            name="name"
            required
            maxLength={120}
            defaultValue={editor === 'new' ? '' : editor.name}
          />
          {!pricesOnly && (
            <>
              <Field
                label="Phone"
                name="phone"
                maxLength={80}
                defaultValue={
                  editor === 'new' ? '' : (editor as Customer).phone
                }
              />
              <Field
                label="Address"
                name="address"
                maxLength={300}
                defaultValue={
                  editor === 'new' ? '' : (editor as Customer).address
                }
              />
            </>
          )}
          <Field
            label="Active"
            name="active"
            type="checkbox"
            defaultChecked={editor === 'new' || editor.active}
          />
          <div className="flex flex-wrap gap-3">
            <Button disabled={busy}>
              {pricesOnly ? 'Save price list' : 'Save customer'}
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={busy}
              onClick={() => setEditor(null)}
            >
              Cancel
            </Button>
          </div>
        </form>
      )}
      <DataTable
        title={pricesOnly ? 'Price lists' : 'Customers'}
        data={rows.data?.items ?? []}
        rowCount={rows.data?.total ?? 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        loading={rows.isPending}
        error={rows.error?.message}
        retry={() => void rows.refetch()}
        columns={[
          {
            header: 'Name',
            accessorKey: 'name',
            cell: ({ row }) => (
              <Button
                variant="ghost"
                onClick={() => {
                  if (pricesOnly) setSelected(row.original);
                  else setCustomer(row.original);
                }}
              >
                {row.original.name}
              </Button>
            ),
          },
          ...(!pricesOnly ? [{ header: 'Phone', accessorKey: 'phone' }] : []),
          {
            header: 'Status',
            cell: ({ row }) => (row.original.active ? 'Active' : 'Inactive'),
          },
          ...(canEdit
            ? [
                {
                  header: 'Actions',
                  cell: ({
                    row,
                  }: {
                    row: { original: Customer & PriceList };
                  }) => (
                    <Button
                      variant="outline"
                      onClick={() => {
                        setEditor(row.original);
                        setError('');
                      }}
                    >
                      Edit {row.original.name}
                    </Button>
                  ),
                },
              ]
            : []),
        ]}
      />
      {!pricesOnly && customer && (
        <section className="panel grid gap-3" aria-label="Customer profile">
          <h2>{customer.name}</h2>
          <dl>
            <dt>Phone</dt>
            <dd>{customer.phone || 'Not provided'}</dd>
            <dt>Address</dt>
            <dd>{customer.address || 'Not provided'}</dd>
            <dt>Status</dt>
            <dd>{customer.active ? 'Active' : 'Inactive'}</dd>
          </dl>
          <Button variant="outline" onClick={() => setCustomer(null)}>
            Close profile
          </Button>
        </section>
      )}
      {pricesOnly && selected && (
        <PriceEditor
          key={`${selected.id}:${selected.version}`}
          user={user}
          list={selected}
          canEdit={canEdit}
          close={() => setSelected(null)}
        />
      )}
    </div>
  );
}

function PriceEditor({
  user,
  list,
  canEdit,
  close,
}: {
  user: Me;
  list: PriceList;
  canEdit: boolean;
  close: () => void;
}) {
  const cache = useQueryClient();
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [editor, setEditor] = useState<Price | 'new' | null>(null);
  const [itemSearch, setItemSearch] = useState('');
  const [itemPage, setItemPage] = useState(1);
  const [item, setItem] = useState<Item | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const prices = useQuery({
    queryKey: ['sales-prices', user.tenantId, list.id, pagination],
    queryFn: () =>
      api<Page<Price>>(
        `${root}/price-lists/${list.id}/prices?page=${pagination.pageIndex + 1}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const items = useQuery({
    queryKey: ['price-items', user.tenantId, itemSearch, itemPage],
    enabled: editor === 'new' && canEdit,
    queryFn: () =>
      api<Page<Item>>(
        `/api/workspace/inventory/items?search=${encodeURIComponent(itemSearch)}&page=${itemPage}&pageSize=20`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError('');
    try {
      const current = editor && editor !== 'new' ? editor : null;
      await api(
        `${root}/price-lists/${list.id}/prices${current ? `/${current.id}` : ''}`,
        current ? 'PUT' : 'POST',
        {
          itemId: current?.itemId ?? item?.id,
          mode: current?.mode ?? value(data, 'mode'),
          unitPrice: value(data, 'price'),
          active: data.has('active'),
          version: current?.version ?? 0,
        },
        user.tenantId,
      );
      setEditor(null);
      await cache.invalidateQueries({ queryKey: ['sales-prices'] });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="panel grid gap-4" aria-label="Price list prices">
      <h2>{list.name} — prices</h2>
      <p>
        PKR selling price per catalog unit for gas and parts; per cylinder for
        cylinder modes. Borrowed-cylinder gas prices exclude refundable
        deposits.
      </p>
      <div className="flex flex-wrap gap-3">
        {canEdit && list.active && (
          <Button
            onClick={() => {
              setEditor('new');
              setItem(null);
              setError('');
            }}
          >
            Add price
          </Button>
        )}
        <Button variant="outline" onClick={close}>
          Close prices
        </Button>
      </div>
      {error && <p role="alert">{error}</p>}
      {editor && (
        <form
          aria-label="Sale price editor"
          className="grid gap-4"
          onSubmit={save}
        >
          {editor === 'new' ? (
            <>
              <Field
                label="Find item"
                value={itemSearch}
                onChange={(e) => {
                  setItemSearch(e.target.value);
                  setItemPage(1);
                }}
              />
              {items.isPending && <p role="status">Loading items…</p>}
              {items.error && <p role="alert">{items.error.message}</p>}
              <div className="flex flex-wrap gap-2">
                {items.data?.items
                  .filter((x) => x.active)
                  .map((x) => (
                    <Button
                      type="button"
                      key={x.id}
                      variant={item?.id === x.id ? 'default' : 'outline'}
                      onClick={() => setItem(x)}
                    >
                      {x.sku} — {x.name}
                    </Button>
                  ))}
              </div>
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="outline"
                  disabled={itemPage === 1}
                  onClick={() => setItemPage((x) => x - 1)}
                >
                  Previous items
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  disabled={itemPage * 20 >= (items.data?.total ?? 0)}
                  onClick={() => setItemPage((x) => x + 1)}
                >
                  Next items
                </Button>
              </div>
              {item && (
                <div className="grid gap-2">
                  <label htmlFor="sale-price-mode">Sale mode</label>
                  <select
                    id="sale-price-mode"
                    key={item.id}
                    name="mode"
                    required
                  >
                    {modes[item.kind]?.map((mode) => (
                      <option key={mode} value={mode}>
                        {labels[mode]}
                      </option>
                    ))}
                  </select>
                </div>
              )}
            </>
          ) : (
            <p>
              {editor.sku} — {labels[editor.mode]}
            </p>
          )}
          <Field
            key={editor === 'new' ? 'new' : editor.id}
            label="Unit price (PKR)"
            name="price"
            type="number"
            min="0"
            max="1000000"
            step="0.000001"
            required
            defaultValue={editor === 'new' ? '' : editor.unitPrice}
          />
          <Field
            key={`active-${editor === 'new' ? 'new' : editor.id}`}
            label="Price active"
            name="active"
            type="checkbox"
            defaultChecked={editor === 'new' || editor.active}
          />
          <div className="flex gap-3">
            <Button disabled={busy || (editor === 'new' && !item)}>
              Save price
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={busy}
              onClick={() => setEditor(null)}
            >
              Cancel price
            </Button>
          </div>
        </form>
      )}
      <DataTable
        title="Selling prices"
        data={prices.data?.items ?? []}
        rowCount={prices.data?.total ?? 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        loading={prices.isPending}
        error={prices.error?.message}
        retry={() => void prices.refetch()}
        columns={[
          { header: 'Item', accessorKey: 'itemName' },
          {
            header: 'Mode',
            cell: ({ row }) => labels[row.original.mode] ?? row.original.mode,
          },
          { header: 'Price', cell: ({ row }) => money(row.original.unitPrice) },
          {
            header: 'Status',
            cell: ({ row }) => (row.original.active ? 'Active' : 'Inactive'),
          },
          ...(canEdit && list.active
            ? [
                {
                  header: 'Actions',
                  cell: ({ row }: { row: { original: Price } }) => (
                    <Button
                      variant="outline"
                      onClick={() => {
                        setEditor(row.original);
                        setError('');
                      }}
                    >
                      Edit price for {row.original.sku}
                    </Button>
                  ),
                },
              ]
            : []),
        ]}
      />
    </section>
  );
}
