import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Eye,
  FileText,
  HandCoins,
  MapPin,
  Pencil,
  Phone,
  Plus,
  Wallet,
} from 'lucide-react';
import { api, type Me } from './access-api';
import { DataTable } from './components/DataTable';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { Modal } from './components/ui/feedback';
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
  Tabs,
  Toolbar,
} from './components/page';
import { useViewFocus } from './lib/ui-helpers';
import { businessDate } from './report-types';
import type { Item, Page } from './inventory-types';
import { useBranch } from './components/branch-context';
import { SalesRegister } from './SalesRegister';
import { CylinderWorkspace, DepositArrangement } from './CylinderWorkspace';

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
  timeZone,
}: {
  user: Me;
  pricesOnly: boolean;
  timeZone: string;
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
  const noun = pricesOnly ? 'price list' : 'customer';
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
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [customer?.id]);
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
  const startEdit = (row: Customer | PriceList | 'new') => {
    setEditor(row);
    setError('');
  };
  const editorDialog = (
    <Modal
      title={
        editor === 'new'
          ? `Add ${noun}`
          : `Edit ${noun}${editor ? ` · ${editor.name}` : ''}`
      }
      description={
        pricesOnly
          ? 'Price lists group selling prices, for example retail or commercial.'
          : 'Contact details appear on sales and receipts.'
      }
      open={!!editor}
      onOpenChange={(open) => {
        if (!open) setEditor(null);
      }}
    >
      {editor && (
        <form
          key={editor === 'new' ? 'new' : editor.id}
          aria-label={pricesOnly ? 'Price list editor' : 'Customer editor'}
          className="grid gap-4"
          onSubmit={save}
        >
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
                inputMode="tel"
                placeholder="03XX-XXXXXXX"
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
          <label className="check">
            <input
              type="checkbox"
              name="active"
              defaultChecked={editor === 'new' || editor.active}
            />
            Active
          </label>
          {error && <p role="alert">{error}</p>}
          <div className="dialog-actions">
            <Button
              type="button"
              variant="outline"
              disabled={busy}
              onClick={() => setEditor(null)}
            >
              Cancel
            </Button>
            <Button disabled={busy}>
              {pricesOnly ? 'Save price list' : 'Save customer'}
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
  if (!pricesOnly && customer)
    return (
      <>
        {notice && (
          <p role="status" className="notice-banner">
            <CheckCircle2 aria-hidden="true" size={18} /> {notice}
          </p>
        )}
        <CustomerProfile
          user={user}
          customer={customer}
          timeZone={timeZone}
          canEdit={canEdit}
          edit={() => startEdit(customer)}
          back={() => {
            setCustomer(null);
            setNotice('');
          }}
        />
        {editorDialog}
      </>
    );
  if (pricesOnly && selected)
    return (
      <PriceEditor
        key={`${selected.id}:${selected.version}`}
        user={user}
        list={selected}
        canEdit={canEdit}
        close={() => {
          setSelected(null);
          setNotice('');
        }}
      />
    );
  const total = rows.data?.total ?? 0;
  return (
    <div className="grid gap-6">
      {canEdit && (
        <PageActions>
          <Button onClick={() => startEdit('new')}>
            <Plus aria-hidden="true" size={18} />
            {pricesOnly ? 'Add price list' : 'Add customer'}
          </Button>
        </PageActions>
      )}
      {notice && (
        <p role="status" className="notice-banner">
          <CheckCircle2 aria-hidden="true" size={18} /> {notice}
        </p>
      )}
      {!editor && error && <p role="alert">{error}</p>}
      <Toolbar
        search={{
          label: pricesOnly ? 'Search price lists' : 'Search customers',
          placeholder: pricesOnly
            ? 'Search price lists by name'
            : 'Search customers by name or phone',
          value: search,
          onChange: (text) => {
            setSearch(text);
            setPagination((p) => ({ ...p, pageIndex: 0 }));
          },
        }}
      >
        <span className="toolbar-count">
          {total} {pricesOnly ? 'price lists' : 'customers'}
        </span>
      </Toolbar>
      {editorDialog}
      {pricesOnly ? (
        <DataTable
          title="Price lists"
          data={rows.data?.items ?? []}
          rowCount={total}
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
                <button
                  type="button"
                  className="table-link"
                  onClick={() => {
                    setSelected(row.original);
                    setNotice('');
                  }}
                >
                  {row.original.name}
                </button>
              ),
            },
            {
              header: 'Status',
              cell: ({ row }) => (
                <StatusChip tone={row.original.active ? 'success' : 'neutral'}>
                  {row.original.active ? 'Active' : 'Inactive'}
                </StatusChip>
              ),
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
                        onClick={() => startEdit(row.original)}
                      >
                        <Pencil aria-hidden="true" size={16} />
                        Edit
                        <span className="sr-only"> {row.original.name}</span>
                      </Button>
                    ),
                  },
                ]
              : []),
          ]}
        />
      ) : rows.isPending ? (
        <PageState
          kind="loading"
          title="Loading customers"
          description="Please wait while customers are loaded."
        />
      ) : rows.error ? (
        <PageState
          kind="error"
          title="Customers unavailable"
          description={rows.error.message}
          retry={() => void rows.refetch()}
        />
      ) : !rows.data.items.length ? (
        <PageState
          kind="empty"
          title={search ? 'No matching customers' : 'No customers yet'}
          description={
            search
              ? 'Try another name or phone number.'
              : 'Add your first customer to start recording sales and credit.'
          }
          action={
            canEdit && !search ? (
              <Button onClick={() => startEdit('new')}>
                <Plus aria-hidden="true" size={18} /> Add your first customer
              </Button>
            ) : undefined
          }
        />
      ) : (
        <section aria-label="Customers" className="grid gap-4">
          <ul className="entity-grid">
            {rows.data.items.map((row) => (
              <li key={row.id} className="entity-card">
                <div className="entity-card-top">
                  <span className="avatar large" aria-hidden="true">
                    {initials(row.name)}
                  </span>
                  <div className="entity-card-title">
                    <button
                      type="button"
                      className="table-link"
                      onClick={() => {
                        setCustomer(row);
                        setNotice('');
                      }}
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
                    onClick={() => {
                      setCustomer(row);
                      setNotice('');
                    }}
                  >
                    <Eye aria-hidden="true" size={16} />
                    View<span className="sr-only"> {row.name} profile</span>
                  </Button>
                  {canEdit && (
                    <Button variant="outline" onClick={() => startEdit(row)}>
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

type Credit = {
  balance: number;
  unapplied: number;
  invoices: { outstanding: number }[];
};

function CustomerProfile({
  user,
  customer,
  timeZone,
  canEdit,
  edit,
  back,
}: {
  user: Me;
  customer: Customer;
  timeZone: string;
  canEdit: boolean;
  edit: () => void;
  back: () => void;
}) {
  const branch = useBranch();
  const heading = useViewFocus<HTMLHeadingElement>();
  const [tab, setTab] = useState('sales');
  const has = (permission: string) => user.permissions.includes(permission);
  const today = businessDate(timeZone);
  const credit = useQuery({
    queryKey: ['customer-credit', user.tenantId, branch, customer.id, today],
    enabled: !!branch && has('finance.view'),
    queryFn: () =>
      api<Credit>(
        `/api/workspace/finance/credit?branchId=${branch}&partyId=${customer.id}&supplier=false&asOf=${today}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const sales = has('sales.view') && !!branch;
  const details = (
    <div className="grid gap-6">
      <Card title="Contact details">
        <KeyValues
          items={[
            ['Phone', customer.phone || 'Not provided'],
            ['Address', customer.address || 'Not provided'],
            ['Status', customer.active ? 'Active' : 'Inactive'],
          ]}
        />
      </Card>
      {has('sales.view') && (
        <Card title="Cylinder deposit arrangement">
          <DepositArrangement
            key={customer.id}
            user={user}
            customerId={customer.id}
          />
        </Card>
      )}
    </div>
  );
  return (
    <section className="detail-page" aria-label="Customer profile">
      <BackButton onClick={back}>Back to customers</BackButton>
      <div className="profile-hero">
        <span className="avatar hero" aria-hidden="true">
          {initials(customer.name)}
        </span>
        <div className="profile-hero-text">
          <h2 ref={heading} tabIndex={-1}>
            {customer.name}{' '}
            <StatusChip tone={customer.active ? 'success' : 'neutral'}>
              {customer.active ? 'Active' : 'Inactive'}
            </StatusChip>
          </h2>
          <ul className="entity-facts inline">
            <li>
              <Phone aria-hidden="true" size={16} />
              {customer.phone || 'No phone'}
            </li>
            <li>
              <MapPin aria-hidden="true" size={16} />
              {customer.address || 'No address'}
            </li>
          </ul>
        </div>
        <div className="detail-actions">
          {has('sales.create') && (
            <Button variant="outline" asChild>
              <Link to="/workspace/sales?new=1">
                <Plus aria-hidden="true" size={16} /> New sales draft
              </Link>
            </Button>
          )}
          {canEdit && (
            <Button onClick={edit}>
              <Pencil aria-hidden="true" size={16} />
              Edit<span className="sr-only"> {customer.name}</span>
            </Button>
          )}
        </div>
      </div>
      {credit.data && (
        <StatGrid label="Customer balance">
          <StatCard
            label="Balance owed"
            icon={HandCoins}
            tone={credit.data.balance > 0 ? 'warning' : 'success'}
            value={money(credit.data.balance)}
            note="Selected branch, net of unapplied payments"
          />
          <StatCard
            label="Unapplied payments"
            icon={Wallet}
            value={money(credit.data.unapplied)}
            note="Advances not yet allocated"
          />
          <StatCard
            label="Open invoices"
            icon={FileText}
            value={credit.data.invoices.filter((x) => x.outstanding > 0).length}
            note="Sales, loans and openings with a balance"
          />
        </StatGrid>
      )}
      {credit.error && <p role="alert">{credit.error.message}</p>}
      {sales ? (
        <Tabs
          label="Customer sections"
          value={tab}
          onChange={setTab}
          tabs={[
            {
              id: 'sales',
              label: 'Sales',
              content: (
                <SalesRegister
                  key={`sales-${branch}-${customer.id}`}
                  user={user}
                  branch={branch}
                  customerId={customer.id}
                />
              ),
            },
            {
              id: 'cylinders',
              label: 'Borrowed cylinders',
              content: (
                <CylinderWorkspace
                  key={`loans-${branch}-${customer.id}`}
                  user={user}
                  branch={branch}
                  customerId={customer.id}
                />
              ),
            },
            { id: 'details', label: 'Details', content: details },
          ]}
        />
      ) : (
        details
      )}
    </section>
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
  const heading = useViewFocus<HTMLHeadingElement>();
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
    <section className="detail-page" aria-label="Price list prices">
      <BackButton onClick={close}>Back to price lists</BackButton>
      <div className="detail-header">
        <div>
          <h2 ref={heading} tabIndex={-1}>
            {list.name} — prices{' '}
            <StatusChip tone={list.active ? 'success' : 'neutral'}>
              {list.active ? 'Active' : 'Inactive'}
            </StatusChip>
          </h2>
          <p>
            PKR selling price per catalog unit for gas and parts; per cylinder
            for cylinder modes. Borrowed-cylinder gas prices exclude refundable
            deposits.
          </p>
        </div>
        {canEdit && list.active && (
          <div className="detail-actions">
            <Button
              onClick={() => {
                setEditor('new');
                setItem(null);
                setError('');
              }}
            >
              <Plus aria-hidden="true" size={16} /> Add price
            </Button>
          </div>
        )}
      </div>
      {!editor && error && <p role="alert">{error}</p>}
      <Modal
        title={editor === 'new' ? 'Add price' : 'Edit price'}
        description="Prices are versioned. Drafts keep the price they were prepared with."
        open={!!editor}
        onOpenChange={(open) => {
          if (!open) setEditor(null);
        }}
      >
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
                  placeholder="Search by SKU or name"
                  onChange={(e) => {
                    setItemSearch(e.target.value);
                    setItemPage(1);
                  }}
                />
                {items.isPending && <p role="status">Loading items…</p>}
                {items.error && <p role="alert">{items.error.message}</p>}
                <div className="option-grid">
                  {items.data?.items
                    .filter((x) => x.active)
                    .map((x) => (
                      <Button
                        type="button"
                        key={x.id}
                        variant="outline"
                        className="option-button"
                        aria-pressed={item?.id === x.id}
                        onClick={() => setItem(x)}
                      >
                        {x.sku} — {x.name}
                      </Button>
                    ))}
                </div>
                {(items.data?.total ?? 0) > 20 && (
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
                )}
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
              <p className="muted-text">
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
            <label className="check">
              <input
                key={`active-${editor === 'new' ? 'new' : editor.id}`}
                type="checkbox"
                name="active"
                defaultChecked={editor === 'new' || editor.active}
              />
              Price active
            </label>
            {error && <p role="alert">{error}</p>}
            <div className="dialog-actions">
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setEditor(null)}
              >
                Cancel price
              </Button>
              <Button disabled={busy || (editor === 'new' && !item)}>
                Save price
              </Button>
            </div>
          </form>
        )}
      </Modal>
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
          {
            header: 'Item',
            cell: ({ row }) => (
              <>
                <strong>{row.original.itemName}</strong>
                <span className="cell-note">{row.original.sku}</span>
              </>
            ),
          },
          {
            header: 'Mode',
            cell: ({ row }) => labels[row.original.mode] ?? row.original.mode,
          },
          {
            header: 'Price',
            cell: ({ row }) => (
              <strong>
                {money(row.original.unitPrice)}
                <span className="cell-note">per {row.original.unit}</span>
              </strong>
            ),
          },
          {
            header: 'Status',
            cell: ({ row }) => (
              <StatusChip tone={row.original.active ? 'success' : 'neutral'}>
                {row.original.active ? 'Active' : 'Inactive'}
              </StatusChip>
            ),
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
                      <Pencil aria-hidden="true" size={16} />
                      Edit
                      <span className="sr-only">
                        {' '}
                        price for {row.original.sku}
                      </span>
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
