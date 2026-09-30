import { useState, useRef, type FormEvent, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Boxes,
  Building2,
  Package,
  PackageX,
  Pencil,
  Plus,
  Ruler,
  Settings2,
  Warehouse as WarehouseIcon,
} from 'lucide-react';
import { api, type Branch, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { Modal, ConfirmButton, Notification } from './components/ui/feedback';
import {
  BackButton,
  Card,
  PageState,
  StatCard,
  StatGrid,
  StatusChip,
  Toolbar,
} from './components/page';
import { PageActions } from './components/page-actions';
import { DataTable } from './components/DataTable';
import { useViewFocus } from './lib/ui-helpers';
import { businessDate, type Dashboard } from './report-types';
import type {
  Unit,
  Category,
  Warehouse,
  Item,
  Page,
  Balance,
  Movement,
  StockDocument,
  Asset,
} from './inventory-types';
import { tr } from './i18n';

const root = '/api/workspace/inventory';
const kindLabels: Record<string, string> = {
  Lpg: 'LPG',
  Cylinder: 'Cylinder',
  SparePart: 'Spare part',
};
function Select({
  label,
  name,
  children,
  value,
  onChange,
}: {
  label: string;
  name?: string;
  children: ReactNode;
  value?: string;
  onChange?: (value: string) => void;
}) {
  return (
    <label className="grid gap-2">
      {label}
      <select
        name={name}
        value={value}
        onChange={onChange ? (e) => onChange(e.target.value) : undefined}
        required
      >
        {children}
      </select>
    </label>
  );
}
function fields(event: FormEvent<HTMLFormElement>) {
  event.preventDefault();
  return new FormData(event.currentTarget);
}
const text = (data: FormData, name: string) => String(data.get(name) || '');
const states = (item: Item) =>
  item.kind === 'Cylinder'
    ? [
        'Full',
        'Empty',
        'InTransit',
        'CustomerHeld',
        'Inspection',
        'Damaged',
        'Lost',
        'Retired',
      ]
    : ['Available', 'Damaged'];

export function InventoryPage({
  user,
  branches,
  timeZone,
}: {
  user: Me;
  branches: Branch[];
  timeZone: string;
}) {
  const branchId = useBranch();
  const client = useQueryClient();
  const [search, setSearch] = useState('');
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [selected, setSelected] = useState<Item | null>(null);
  const setupTrigger = useRef<HTMLButtonElement>(null);
  const createTrigger = useRef<HTMLButtonElement>(null);
  const [setup, setSetup] = useState(false);
  const [editing, setEditing] = useState<Item | null>(null);
  const [create, setCreate] = useState(false);
  const [kind, setKind] = useState('Lpg');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState(0);
  const manage = user.permissions.includes('catalog.manage');
  const get = <T,>(path: string) =>
    api<T>(root + path, 'GET', undefined, user.tenantId);
  const config = useQuery({
    queryKey: ['inventory-config', user.tenantId],
    queryFn: async () => {
      const [units, categories, warehouses] = await Promise.all([
        get<Unit[]>('/units'),
        get<Category[]>('/categories'),
        get<Warehouse[]>('/warehouses'),
      ]);
      return { units, categories, warehouses };
    },
  });
  const items = useQuery({
    queryKey: ['inventory-items', user.tenantId, search, pagination],
    queryFn: () =>
      get<Page<Item>>(
        `/items?search=${encodeURIComponent(search)}&page=${pagination.pageIndex + 1}&pageSize=${pagination.pageSize}`,
      ),
  });
  const today = businessDate(timeZone);
  const figures = useQuery({
    queryKey: ['dashboard', user.tenantId, today, today, branchId],
    enabled: !!branchId && user.permissions.includes('reports.view'),
    queryFn: () =>
      api<Dashboard>(
        `/api/workspace/reports/dashboard?from=${today}&to=${today}&branchId=${branchId}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  async function save(
    path: string,
    body: unknown,
    onSuccess?: () => void,
    method = 'POST',
  ) {
    setBusy(true);
    setError('');
    try {
      await api(root + path, method, body, user.tenantId);
      await client.invalidateQueries({
        predicate: (q) => String(q.queryKey[0]).startsWith('inventory'),
      });
      setNotice((n) => n + 1);
      onSuccess?.();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not save.');
    } finally {
      setBusy(false);
    }
  }
  if (config.isError)
    return (
      <PageState
        kind="error"
        title={tr('Inventory unavailable')}
        description={config.error.message}
        retry={() => void config.refetch()}
      />
    );
  if (!config.data)
    return (
      <PageState
        kind="loading"
        title={tr('Loading inventory')}
        description={tr('Retrieving catalog settings.')}
      />
    );
  const { units, categories, warehouses } = config.data;
  const branchWarehouses = warehouses.filter(
    (w) => w.branchId === branchId && w.active,
  );
  const lowStock = figures.data?.metrics.find((m) => m.key === 'lowStock');
  return (
    <div className="grid gap-6">
      {manage && !selected && (
        <PageActions>
          <Button
            ref={setupTrigger}
            variant="outline"
            onClick={() => setSetup(true)}
          >
            <Settings2 aria-hidden="true" size={18} /> {tr('Catalog setup')}
          </Button>
          <Button ref={createTrigger} onClick={() => setCreate(true)}>
            <Plus aria-hidden="true" size={18} /> {tr('Add item')}
          </Button>
        </PageActions>
      )}
      {error && !setup && !create && !editing && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {notice > 0 && (
        <Notification key={notice} message="Inventory changes saved." />
      )}
      {selected ? (
        <StockDetail
          key={`${selected.id}:${branchId}`}
          item={selected}
          user={user}
          warehouses={warehouses.filter((w) => w.branchId === branchId)}
          allWarehouses={warehouses}
          timeZone={timeZone}
          onClose={() => setSelected(null)}
          onEdit={() => setEditing(selected)}
        />
      ) : (
        <>
          <StatGrid label={tr('Stock summary')}>
            <StatCard
              label={tr('Catalog items')}
              icon={Package}
              value={items.data?.total ?? '—'}
              note={
                search
                  ? tr('Matching your search')
                  : tr('LPG, cylinders and parts')
              }
            />
            <StatCard
              label={tr('Warehouses in this branch')}
              icon={WarehouseIcon}
              value={branchWarehouses.length}
              note={`${warehouses.length} across all branches`}
            />
            {lowStock && (
              <StatCard
                label={tr('At or below reorder level')}
                icon={PackageX}
                tone={lowStock.value > 0 ? 'warning' : 'success'}
                value={lowStock.value}
                note={tr('Selected branch')}
              />
            )}
            <StatCard
              label={tr('Stock units')}
              icon={Ruler}
              value={units.length}
              note={units.map((u) => u.code).join(', ') || tr('None yet')}
            />
          </StatGrid>
          <Toolbar
            search={{
              label: 'Search catalog',
              placeholder: 'Search by SKU or item name',
              value: search,
              onChange: (value) => {
                setSearch(value);
                setPagination((p) => ({ ...p, pageIndex: 0 }));
              },
            }}
          />
          <DataTable
            title={tr('Stock directory')}
            data={items.data?.items || []}
            rowCount={items.data?.total || 0}
            pagination={pagination}
            onPaginationChange={setPagination}
            loading={items.isPending}
            error={items.error?.message}
            retry={() => void items.refetch()}
            columns={[
              {
                accessorKey: 'name',
                header: 'Item',
                cell: ({ row }) => (
                  <>
                    <button
                      type="button"
                      className="table-link"
                      onClick={() => setSelected(row.original)}
                    >
                      {row.original.name}
                    </button>
                    <span className="cell-note">{row.original.sku}</span>
                  </>
                ),
              },
              {
                accessorKey: 'kind',
                header: 'Kind',
                cell: ({ row }) => (
                  <span className="kind-tag">
                    {tr(kindLabels[row.original.kind] ?? row.original.kind)}
                  </span>
                ),
              },
              {
                id: 'tracking',
                header: 'Tracking',
                cell: ({ row }) =>
                  row.original.serialTracked ? 'Serials' : 'Quantity',
              },
              { accessorKey: 'reorderLevel', header: 'Reorder level' },
              {
                accessorKey: 'active',
                header: 'Status',
                cell: ({ row }) => (
                  <StatusChip
                    tone={row.original.active ? 'success' : 'neutral'}
                  >
                    {row.original.active ? tr('Active') : tr('Inactive')}
                  </StatusChip>
                ),
              },
            ]}
          />
        </>
      )}
      <Modal
        title={tr('Edit item')}
        description={tr(
          'Update catalog display details. Stock identity and posted movements remain fixed.',
        )}
        open={!!editing}
        onOpenChange={(open) => {
          if (!open) setEditing(null);
        }}
      >
        {error && <p role="alert">{error}</p>}
        {editing && (
          <form
            key={editing.id}
            className="grid gap-4"
            onSubmit={(event) => {
              const d = fields(event);
              void save(
                `/items/${editing.id}`,
                {
                  ...editing,
                  name: text(d, 'name'),
                  brand: text(d, 'brand'),
                  reorderLevel: text(d, 'reorder'),
                  active: d.get('active') === 'on',
                },
                () => {
                  setEditing(null);
                  setSelected(null);
                },
                'PUT',
              );
            }}
          >
            <p className="muted-text">
              {editing.sku} · {tr(kindLabels[editing.kind] ?? editing.kind)}
            </p>
            <Field
              label={tr('Item name')}
              name="name"
              defaultValue={editing.name}
              required
            />
            <div className="form-grid">
              <Field
                label={tr('Brand')}
                name="brand"
                defaultValue={editing.brand}
              />
              <Field
                label={tr('Reorder level')}
                name="reorder"
                type="number"
                min="0"
                step="0.001"
                defaultValue={editing.reorderLevel}
                required
              />
            </div>
            <label className="check">
              <input
                type="checkbox"
                name="active"
                defaultChecked={editing.active}
              />
              {tr('Active item')}
            </label>
            <div className="dialog-actions">
              <Button
                type="button"
                variant="outline"
                onClick={() => setEditing(null)}
              >
                {tr('Cancel')}
              </Button>
              <Button disabled={busy}>{tr('Save item')}</Button>
            </div>
          </form>
        )}
      </Modal>
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          setupTrigger.current?.focus({ preventScroll: true });
        }}
        title={tr('Catalog setup')}
        description={tr(
          'Define units, spare-part categories and branch storage locations.',
        )}
        open={setup}
        onOpenChange={setSetup}
      >
        {error && <p role="alert">{error}</p>}
        <div className="grid gap-4">
          <form
            className="setup-section"
            onSubmit={(e) => {
              const d = fields(e);
              void save('/units', {
                code: text(d, 'code'),
                decimalPlaces: Number(text(d, 'precision')),
              });
            }}
          >
            <h3>
              <Ruler aria-hidden="true" size={18} /> {tr('Units')}
            </h3>
            <p className="muted-text">
              {units
                .map((u) => `${u.code} (${u.decimalPlaces} decimals)`)
                .join(', ') || tr('No units yet.')}
            </p>
            <div className="form-grid">
              <Field
                label={tr('Unit code')}
                name="code"
                required
                maxLength={20}
              />
              <Field
                label={tr('Decimal places')}
                name="precision"
                type="number"
                min="0"
                max="3"
                defaultValue="3"
                required
              />
            </div>
            <Button variant="outline" disabled={busy}>
              {tr('Add unit')}
            </Button>
          </form>
          <form
            className="setup-section"
            onSubmit={(e) => {
              const d = fields(e);
              void save('/categories', { name: text(d, 'name') });
            }}
          >
            <h3>
              <Boxes aria-hidden="true" size={18} />{' '}
              {tr('Spare-part categories')}
            </h3>
            <p className="muted-text">
              {categories.map((c) => c.name).join(', ') ||
                tr('No categories yet.')}
            </p>
            <Field label={tr('Category name')} name="name" required />
            <Button variant="outline" disabled={busy}>
              {tr('Add category')}
            </Button>
          </form>
          <form
            className="setup-section"
            onSubmit={(e) => {
              const d = fields(e);
              void save('/warehouses', {
                branchId: text(d, 'branch'),
                name: text(d, 'name'),
                location: text(d, 'location'),
              });
            }}
          >
            <h3>
              <Building2 aria-hidden="true" size={18} />{' '}
              {tr('Warehouses and locations')}
            </h3>
            <ul className="setup-list">
              {warehouses.map((w) => (
                <li key={w.id}>
                  {w.name} · {branches.find((b) => b.id === w.branchId)?.name} ·{' '}
                  {w.location}
                </li>
              ))}
            </ul>
            <Select label={tr('Branch')} name="branch">
              {branches
                .filter((b) => b.active)
                .map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
            </Select>
            <div className="form-grid">
              <Field label={tr('Warehouse name')} name="name" required />
              <Field
                label={tr('Location / rack description')}
                name="location"
              />
            </div>
            <Button variant="outline" disabled={busy}>
              {tr('Add warehouse')}
            </Button>
          </form>
        </div>
      </Modal>
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          createTrigger.current?.focus({ preventScroll: true });
        }}
        title={tr('Add catalog item')}
        description={tr(
          'Set stock identity carefully. SKU, unit, kind and serial tracking are fixed after creation.',
        )}
        open={create}
        onOpenChange={setCreate}
      >
        {error && <p role="alert">{error}</p>}
        <form
          className="grid gap-4"
          onSubmit={(e) => {
            const d = fields(e);
            void save(
              '/items',
              {
                sku: text(d, 'sku'),
                name: text(d, 'name'),
                kind,
                unitId: text(d, 'unit'),
                categoryId: text(d, 'category') || null,
                brand: text(d, 'brand'),
                classification: text(d, 'classification'),
                capacityKg: text(d, 'capacity') || null,
                tareWeightKg: text(d, 'tare') || null,
                serialTracked: d.get('serial') === 'on',
                reorderLevel: text(d, 'reorder') || '0',
              },
              () => setCreate(false),
            );
          }}
        >
          <div className="form-grid">
            <Field label={tr('SKU')} name="sku" required maxLength={40} />
            <Field label={tr('Item name')} name="name" required />
            <Select label={tr('Item kind')} value={kind} onChange={setKind}>
              {['Lpg', 'Cylinder', 'SparePart'].map((k) => (
                <option key={k} value={k}>
                  {tr(kindLabels[k])}
                </option>
              ))}
            </Select>
            <Select label={tr('Stock unit')} name="unit">
              <option value="">{tr('Choose a unit')}</option>
              {units
                .filter((u) => kind !== 'Cylinder' || u.decimalPlaces === 0)
                .map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.code}
                  </option>
                ))}
            </Select>
            {kind === 'SparePart' ? (
              <Select label={tr('Category')} name="category">
                <option value="">{tr('Choose a category')}</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Select>
            ) : (
              <>
                <Field label={tr('Brand')} name="brand" />
                <Select label={tr('Classification')} name="classification">
                  <option>{tr('Domestic')}</option>
                  <option>{tr('Commercial')}</option>
                </Select>
                <Field
                  label={tr('Nominal capacity (kg)')}
                  name="capacity"
                  type="number"
                  min="0.001"
                  step="0.001"
                  required
                />
              </>
            )}
            {kind === 'Cylinder' && (
              <Field
                label={tr('Tare weight (kg)')}
                name="tare"
                type="number"
                min="0"
                step="0.001"
              />
            )}
            <Field
              label={tr('Reorder level')}
              name="reorder"
              type="number"
              min="0"
              step="0.001"
              defaultValue="0"
            />
          </div>
          {kind === 'Cylinder' && (
            <label className="check">
              <input type="checkbox" name="serial" />
              {tr('Track individual serials')}
            </label>
          )}
          <div className="dialog-actions">
            <Button
              type="button"
              variant="outline"
              onClick={() => setCreate(false)}
            >
              {tr('Cancel')}
            </Button>
            <Button disabled={busy || !units.length}>
              {tr('Create item')}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

function StockDetail({
  item,
  user,
  warehouses,
  allWarehouses,
  timeZone,
  onClose,
  onEdit,
}: {
  item: Item;
  user: Me;
  warehouses: Warehouse[];
  allWarehouses: Warehouse[];
  timeZone: string;
  onClose: () => void;
  onEdit: () => void;
}) {
  const client = useQueryClient();
  const branchId = useBranch();
  const heading = useViewFocus<HTMLHeadingElement>();
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [warehouseId, setWarehouseId] = useState(
    warehouses.find((w) => w.active)?.id || '',
  );
  const [destination, setDestination] = useState(
    warehouses.find((w) => w.active)?.id || '',
  );
  const [kind, setKind] = useState('Opening');
  const [document, setDocument] = useState<StockDocument | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [postOpen, setPostOpen] = useState(false);
  const [pending, setPending] = useState<Record<string, unknown> | null>(null);
  const [reverseKey, setReverseKey] = useState(crypto.randomUUID());
  const postTrigger = useRef<HTMLButtonElement>(null);
  const documentTrigger = useRef<HTMLElement | null>(null);
  const [reverseReason, setReverseReason] = useState('');
  const get = <T,>(path: string) =>
    api<T>(root + path, 'GET', undefined, user.tenantId);
  const balances = useQuery({
    queryKey: ['inventory-balances', user.tenantId, item.id],
    queryFn: () => get<Balance[]>(`/items/${item.id}/balances`),
  });
  const ledger = useQuery({
    queryKey: [
      'inventory-ledger',
      user.tenantId,
      item.id,
      warehouseId,
      pagination,
    ],
    enabled: !!warehouseId,
    queryFn: () =>
      get<Page<Movement>>(
        `/items/${item.id}/ledger?warehouseId=${warehouseId}&page=${pagination.pageIndex + 1}&pageSize=${pagination.pageSize}`,
      ),
  });
  const assets = useQuery({
    queryKey: ['inventory-assets', user.tenantId, item.id],
    enabled: item.serialTracked && user.permissions.includes('catalog.manage'),
    queryFn: () => get<Asset[]>(`/items/${item.id}/assets`),
  });
  async function action(path: string, body: unknown, posted = false) {
    setBusy(true);
    setError('');
    try {
      const result = await api<StockDocument>(
        root + path,
        'POST',
        body,
        user.tenantId,
      );
      if (posted) {
        if (path === '/documents')
          documentTrigger.current = postTrigger.current;
        setDocument(result);
        setPending(null);
        setPostOpen(false);
        setReverseKey(crypto.randomUUID());
      }
      await client.invalidateQueries({
        predicate: (q) => String(q.queryKey[0]).startsWith('inventory'),
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Request failed.');
    } finally {
      setBusy(false);
    }
  }
  const visible = (balances.data || []).filter((b) => b.branchId === branchId);
  const sequence = (id: string) =>
    (balances.data || []).find((b) => b.warehouseId === id)?.sequence || 0;
  return (
    <section className="detail-page" aria-label={tr('Stock detail')}>
      <BackButton onClick={onClose}>{tr('Back to inventory')}</BackButton>
      <div className="detail-header">
        <div>
          <h2 ref={heading} tabIndex={-1}>
            {item.name}{' '}
            <StatusChip tone={item.active ? 'success' : 'neutral'}>
              {item.active ? tr('Active') : tr('Inactive')}
            </StatusChip>
          </h2>
          <p>
            {item.sku} · {tr(kindLabels[item.kind] ?? item.kind)} ·{' '}
            {item.serialTracked ? tr('Serial tracked') : tr('Quantity tracked')}{' '}
            {tr('· Reorder at')} {item.reorderLevel}
          </p>
        </div>
        {user.permissions.includes('catalog.manage') && (
          <div className="detail-actions">
            <Button variant="outline" onClick={onEdit}>
              <Pencil aria-hidden="true" size={16} /> {tr('Edit item')}
            </Button>
          </div>
        )}
      </div>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      <Card title={tr('Balances in the active branch')}>
        <p className="section-help">
          {tr(
            'Stock is calculated from posted movements. Values and average costs are for the whole product at each warehouse, across all states.',
          )}
        </p>
        {balances.isError ? (
          <PageState
            kind="error"
            title={tr('Balances unavailable')}
            description={balances.error.message}
            retry={() => void balances.refetch()}
          />
        ) : balances.isPending ? (
          <p role="status">{tr('Loading balances…')}</p>
        ) : !visible.length ? (
          <p className="muted-text pt-3">
            {tr('No stock movements in the active branch.')}
          </p>
        ) : (
          <div className="balance-grid">
            {visible.map((b) => (
              <div
                key={`${b.warehouseId}:${b.bucket}`}
                className="balance-card"
              >
                <h3>
                  {warehouses.find((w) => w.id === b.warehouseId)?.name} ·{' '}
                  {b.bucket}
                </h3>
                <p className="balance-quantity">
                  {tr('Quantity:')} {b.quantity}
                </p>
                <p>
                  {tr('Warehouse value: PKR')} {b.warehouseValue.toFixed(2)}
                </p>
                <p>
                  {tr('Average unit cost: PKR')} {b.averageCost.toFixed(6)}
                </p>
              </div>
            ))}
          </div>
        )}
      </Card>
      <Card title={tr('Movement ledger')}>
        <div className="toolbar ledger-toolbar">
          <Select
            label={tr('Ledger warehouse')}
            value={warehouseId}
            onChange={(v) => {
              setWarehouseId(v);
              setPagination((p) => ({ ...p, pageIndex: 0 }));
            }}
          >
            <option value="">{tr('Choose warehouse')}</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </Select>
          {user.permissions.includes('inventory.post') && (
            <Button
              ref={postTrigger}
              disabled={!warehouseId || balances.isPending || balances.isError}
              onClick={() => {
                setPending(null);
                setPostOpen(true);
              }}
            >
              <Plus aria-hidden="true" size={16} />
              {tr('Post stock movement')}
            </Button>
          )}
        </div>
        {warehouseId ? (
          <DataTable
            title={tr('Stock movement ledger')}
            data={ledger.data?.items || []}
            rowCount={ledger.data?.total || 0}
            pagination={pagination}
            onPaginationChange={setPagination}
            loading={ledger.isPending}
            error={ledger.error?.message}
            retry={() => void ledger.refetch()}
            columns={[
              { accessorKey: 'sequence', header: 'Entry' },
              { accessorKey: 'bucket', header: 'State' },
              { accessorKey: 'quantity', header: 'Quantity' },
              { accessorKey: 'value', header: 'Value (PKR)' },
              { accessorKey: 'unitCost', header: 'Unit cost (PKR)' },
              {
                id: 'document',
                header: 'Document',
                cell: ({ row }) => (
                  <button
                    type="button"
                    className="table-link"
                    onClick={(event) => {
                      documentTrigger.current = event.currentTarget;
                      setError('');
                      void get<StockDocument>(
                        `/documents/${row.original.documentId}`,
                      )
                        .then((d) => {
                          setDocument(d);
                          setReverseKey(crypto.randomUUID());
                        })
                        .catch((e) => setError(e.message));
                    }}
                  >
                    {tr('View document')}
                  </button>
                ),
              },
            ]}
          />
        ) : (
          <p className="muted-text">
            {tr('Choose a warehouse to see its movements.')}
          </p>
        )}
      </Card>
      {item.serialTracked && user.permissions.includes('catalog.manage') && (
        <Card title={tr('Cylinder assets and inspections')}>
          {assets.error && <p role="alert">{assets.error.message}</p>}
          <ul className="setup-list">
            {assets.data?.map((a) => (
              <li key={a.id}>
                {a.serial} · {a.owner} {tr('· Inspection')}{' '}
                {a.inspectionReference || tr('not recorded')}{' '}
                {a.inspectionDue || ''}
              </li>
            ))}
          </ul>
          <details className="pt-3">
            <summary>{tr('Register a cylinder')}</summary>
            <form
              className="grid gap-3 mt-4"
              onSubmit={(e) => {
                const d = fields(e);
                void action('/assets', {
                  itemId: item.id,
                  serial: text(d, 'serial'),
                  owner: text(d, 'owner'),
                  inspectionReference: text(d, 'inspection'),
                  inspectionDue: text(d, 'due') || null,
                });
              }}
            >
              <div className="form-grid">
                <Field label={tr('Cylinder serial')} name="serial" required />
                <Field label={tr('Cylinder owner')} name="owner" required />
                <Field label={tr('Inspection reference')} name="inspection" />
                <Field
                  label={tr('Next inspection date')}
                  name="due"
                  type="date"
                />
              </div>
              <div>
                <Button disabled={busy}>{tr('Register cylinder')}</Button>
              </div>
            </form>
          </details>
        </Card>
      )}
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          if (!document) postTrigger.current?.focus({ preventScroll: true });
        }}
        title={tr('Post stock movement')}
        description={tr(
          'Posting is permanent. Corrections use a separate reversal. The server determines the business date and valuation.',
        )}
        open={postOpen}
        onOpenChange={(v) => {
          if (!busy) setPostOpen(v);
        }}
      >
        {error && <p role="alert">{error}</p>}
        {!pending ? (
          <form
            className="grid gap-4"
            onSubmit={(e) => {
              const d = fields(e);
              setPending({
                idempotencyKey: crypto.randomUUID(),
                kind,
                itemId: item.id,
                warehouseId,
                bucket: text(d, 'bucket'),
                quantity: text(d, 'quantity'),
                unitCost:
                  kind === 'Opening' || kind === 'AdjustmentIn'
                    ? text(d, 'cost')
                    : null,
                destinationWarehouseId:
                  kind === 'Transfer' ? destination : null,
                destinationBucket:
                  kind === 'Transfer' ? text(d, 'destinationBucket') : null,
                reason: text(d, 'reason'),
                expectedSequence: sequence(warehouseId),
                destinationExpectedSequence:
                  kind === 'Transfer' ? sequence(destination) : 0,
                serials: text(d, 'serials')
                  .split(',')
                  .map((s) => s.trim())
                  .filter(Boolean),
              });
            }}
          >
            <p className="muted-text">
              {tr('From')} {warehouses.find((w) => w.id === warehouseId)?.name}
            </p>
            <div className="form-grid">
              <Select
                label={tr('Movement kind')}
                value={kind}
                onChange={setKind}
              >
                {['Opening', 'AdjustmentIn', 'AdjustmentOut', 'Transfer'].map(
                  (k) => (
                    <option key={k}>{k}</option>
                  ),
                )}
              </Select>
              <Select label={tr('Source state')} name="bucket">
                {states(item).map((s) => (
                  <option key={s}>{s}</option>
                ))}
              </Select>
              <Field
                label={tr('Quantity')}
                name="quantity"
                type="number"
                min="0.001"
                step="0.001"
                required
              />
              {(kind === 'Opening' || kind === 'AdjustmentIn') && (
                <Field
                  label={tr('Receipt unit cost (PKR)')}
                  name="cost"
                  type="number"
                  min="0"
                  step="0.000001"
                  required
                />
              )}
              {kind === 'Transfer' && (
                <>
                  <Select
                    label={tr('Destination warehouse')}
                    value={destination}
                    onChange={setDestination}
                  >
                    <option value="">{tr('Choose warehouse')}</option>
                    {allWarehouses
                      .filter((w) => w.active)
                      .map((w) => (
                        <option key={w.id} value={w.id}>
                          {w.name}
                        </option>
                      ))}
                  </Select>
                  <Select
                    label={tr('Destination state')}
                    name="destinationBucket"
                  >
                    {states(item).map((s) => (
                      <option key={s}>{s}</option>
                    ))}
                  </Select>
                </>
              )}
            </div>
            {item.serialTracked && (
              <Field
                label={tr('Serials (comma separated)')}
                name="serials"
                required
              />
            )}
            <Field
              label={tr('Posting reason / opening reconciliation reference')}
              name="reason"
              required
              maxLength={300}
            />
            <div className="dialog-actions">
              <Button>{tr('Review movement')}</Button>
            </div>
          </form>
        ) : (
          <div className="grid gap-4">
            <div className="review-box">
              <p>
                <strong>
                  {String(pending.kind)} {tr('· Quantity')}{' '}
                  {String(pending.quantity)} · {String(pending.bucket)}
                </strong>
              </p>
              <p>
                {tr('Source:')}{' '}
                {allWarehouses.find((w) => w.id === pending.warehouseId)?.name}{' '}
                · {String(pending.bucket)}
              </p>
              {pending.destinationWarehouseId ? (
                <p>
                  {tr('Destination:')}{' '}
                  {
                    allWarehouses.find(
                      (w) => w.id === pending.destinationWarehouseId,
                    )?.name
                  }{' '}
                  · {String(pending.destinationBucket)}
                </p>
              ) : null}
              {pending.unitCost !== null && (
                <p>
                  {tr('Receipt unit cost: PKR')} {String(pending.unitCost)}
                </p>
              )}
              <p>{String(pending.reason)}</p>
            </div>
            <p className="section-help">
              {tr(
                'No totals are calculated in the browser. Retrying this submission uses the same command key.',
              )}
            </p>
            <div className="dialog-actions">
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => setPending(null)}
              >
                {tr('Edit movement')}
              </Button>
              <Button
                disabled={busy}
                onClick={() => void action('/documents', pending, true)}
              >
                {tr('Confirm posting')}
              </Button>
            </div>
          </div>
        )}
      </Modal>
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          (documentTrigger.current?.isConnected
            ? documentTrigger.current
            : postTrigger.current
          )?.focus({ preventScroll: true });
        }}
        title={tr('Stock document')}
        description={tr(
          'Immutable movements and their original cost snapshots.',
        )}
        open={!!document}
        onOpenChange={(open) => {
          if (!open) setDocument(null);
        }}
      >
        {error && <p role="alert">{error}</p>}
        {document && (
          <div className="grid gap-4">
            <div className="review-box">
              <p>
                <strong>
                  {document.kind} ·{' '}
                  {document.reversedById ? tr('Reversed') : tr('Posted')}
                </strong>
              </p>
              <p className="cell-note">{document.id}</p>
              <p>
                {new Intl.DateTimeFormat(undefined, {
                  dateStyle: 'medium',
                  timeStyle: 'short',
                  timeZone,
                }).format(new Date(document.postedAt))}{' '}
                {tr('· Business date')} {document.businessDate}
              </p>
              <p>{document.reason}</p>
            </div>
            <ul className="setup-list">
              {document.movements.map((m) => (
                <li key={m.id}>
                  {allWarehouses.find((w) => w.id === m.warehouseId)?.name} ·{' '}
                  {m.bucket}: {m.quantity} {tr('· PKR')} {m.value.toFixed(2)}{' '}
                  {m.serials.join(', ')}
                </li>
              ))}
            </ul>
            {!document.reversedById &&
              !document.reversesId &&
              user.permissions.includes('inventory.reverse') && (
                <form
                  className="grid gap-3"
                  onSubmit={(e) => e.preventDefault()}
                >
                  <Field
                    label={tr('Reversal reason')}
                    id="reversal-reason"
                    value={reverseReason}
                    onChange={(event) => setReverseReason(event.target.value)}
                    required
                    maxLength={300}
                  />
                  <div>
                    <ConfirmButton
                      destructive
                      disabled={busy || !reverseReason.trim()}
                      title={tr('Reverse this stock document?')}
                      description={tr(
                        'An equal and opposite entry will be posted. Later dependent movements prevent reversal.',
                      )}
                      onConfirm={() => {
                        void action(
                          `/documents/${document.id}/reverse`,
                          { idempotencyKey: reverseKey, reason: reverseReason },
                          true,
                        );
                      }}
                    >
                      {tr('Reverse document')}
                    </ConfirmButton>
                  </div>
                </form>
              )}
          </div>
        )}
      </Modal>
    </section>
  );
}
