import { useState, useRef, type FormEvent, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Branch, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { Modal, ConfirmButton, Notification } from './components/ui/feedback';
import { PageState } from './components/page';
import { DataTable } from './components/DataTable';
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

const root = '/api/workspace/inventory';
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
        title="Inventory unavailable"
        description={config.error.message}
        retry={() => void config.refetch()}
      />
    );
  if (!config.data)
    return (
      <PageState
        kind="loading"
        title="Loading inventory"
        description="Retrieving catalog settings."
      />
    );
  const { units, categories, warehouses } = config.data;
  return (
    <div className="grid gap-6">
      {error && (
        <p role="alert" className="field-error">
          {error}
        </p>
      )}
      {notice > 0 && (
        <Notification key={notice} message="Inventory changes saved." />
      )}
      <div className="flex flex-wrap gap-3 items-end">
        <Field
          label="Search catalog"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPagination((p) => ({ ...p, pageIndex: 0 }));
          }}
        />
        {manage && (
          <>
            <Button ref={createTrigger} onClick={() => setCreate(true)}>
              Add item
            </Button>
            <Button
              ref={setupTrigger}
              variant="outline"
              onClick={() => setSetup(true)}
            >
              Catalog setup
            </Button>
          </>
        )}
      </div>
      <DataTable
        title="Stock directory"
        data={items.data?.items || []}
        rowCount={items.data?.total || 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        loading={items.isPending}
        error={items.error?.message}
        retry={() => void items.refetch()}
        columns={[
          { accessorKey: 'sku', header: 'SKU' },
          {
            accessorKey: 'name',
            header: 'Item',
            cell: ({ row }) => (
              <Button variant="ghost" onClick={() => setSelected(row.original)}>
                {row.original.name}
              </Button>
            ),
          },
          { accessorKey: 'kind', header: 'Kind' },
          {
            accessorKey: 'active',
            header: 'Status',
            cell: ({ row }) => (row.original.active ? 'Active' : 'Inactive'),
          },
          { accessorKey: 'reorderLevel', header: 'Reorder level' },
        ]}
      />
      {selected && (
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
      )}
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          createTrigger.current?.focus({ preventScroll: true });
        }}
        title="Edit item"
        description="Update catalog display details. Stock identity and posted movements remain fixed."
        open={!!editing}
        onOpenChange={(open) => {
          if (!open) setEditing(null);
        }}
      >
        {error && <p role="alert">{error}</p>}
        {editing && (
          <form
            key={editing.id}
            className="grid gap-3"
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
            <p>
              {editing.sku} · {editing.kind}
            </p>
            <Field
              label="Item name"
              name="name"
              defaultValue={editing.name}
              required
            />
            <Field label="Brand" name="brand" defaultValue={editing.brand} />
            <Field
              label="Reorder level"
              name="reorder"
              type="number"
              min="0"
              step="0.001"
              defaultValue={editing.reorderLevel}
              required
            />
            <label>
              <input
                type="checkbox"
                name="active"
                defaultChecked={editing.active}
              />{' '}
              Active item
            </label>
            <Button disabled={busy}>Save item</Button>
          </form>
        )}
      </Modal>
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          setupTrigger.current?.focus({ preventScroll: true });
        }}
        title="Catalog setup"
        description="Define units, spare-part categories and branch storage locations."
        open={setup}
        onOpenChange={setSetup}
      >
        {error && <p role="alert">{error}</p>}
        <div className="grid gap-6">
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              const d = fields(e);
              void save('/units', {
                code: text(d, 'code'),
                decimalPlaces: Number(text(d, 'precision')),
              });
            }}
          >
            <h3>Units</h3>
            <p>
              {units
                .map((u) => `${u.code} (${u.decimalPlaces} decimals)`)
                .join(', ') || 'No units yet.'}
            </p>
            <Field label="Unit code" name="code" required maxLength={20} />
            <Field
              label="Decimal places"
              name="precision"
              type="number"
              min="0"
              max="3"
              defaultValue="3"
              required
            />
            <Button disabled={busy}>Add unit</Button>
          </form>
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              const d = fields(e);
              void save('/categories', { name: text(d, 'name') });
            }}
          >
            <h3>Spare-part categories</h3>
            <p>
              {categories.map((c) => c.name).join(', ') || 'No categories yet.'}
            </p>
            <Field label="Category name" name="name" required />
            <Button disabled={busy}>Add category</Button>
          </form>
          <form
            className="grid gap-3"
            onSubmit={(e) => {
              const d = fields(e);
              void save('/warehouses', {
                branchId: text(d, 'branch'),
                name: text(d, 'name'),
                location: text(d, 'location'),
              });
            }}
          >
            <h3>Warehouses and locations</h3>
            <ul>
              {warehouses.map((w) => (
                <li key={w.id}>
                  {w.name} · {branches.find((b) => b.id === w.branchId)?.name} ·{' '}
                  {w.location}
                </li>
              ))}
            </ul>
            <Select label="Branch" name="branch">
              {branches
                .filter((b) => b.active)
                .map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
            </Select>
            <Field label="Warehouse name" name="name" required />
            <Field label="Location / rack description" name="location" />
            <Button disabled={busy}>Add warehouse</Button>
          </form>
        </div>
      </Modal>
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          createTrigger.current?.focus({ preventScroll: true });
        }}
        title="Add catalog item"
        description="Set stock identity carefully. SKU, unit, kind and serial tracking are fixed after creation."
        open={create}
        onOpenChange={setCreate}
      >
        {error && <p role="alert">{error}</p>}
        <form
          className="grid gap-3"
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
          <Field label="SKU" name="sku" required maxLength={40} />
          <Field label="Item name" name="name" required />
          <Select label="Item kind" value={kind} onChange={setKind}>
            {['Lpg', 'Cylinder', 'SparePart'].map((k) => (
              <option key={k}>{k}</option>
            ))}
          </Select>
          <Select label="Stock unit" name="unit">
            <option value="">Choose a unit</option>
            {units
              .filter((u) => kind !== 'Cylinder' || u.decimalPlaces === 0)
              .map((u) => (
                <option key={u.id} value={u.id}>
                  {u.code}
                </option>
              ))}
          </Select>
          {kind === 'SparePart' ? (
            <Select label="Category" name="category">
              <option value="">Choose a category</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          ) : (
            <>
              <Field label="Brand" name="brand" />
              <Select label="Classification" name="classification">
                <option>Domestic</option>
                <option>Commercial</option>
              </Select>
              <Field
                label="Nominal capacity (kg)"
                name="capacity"
                type="number"
                min="0.001"
                step="0.001"
                required
              />
            </>
          )}
          {kind === 'Cylinder' && (
            <>
              <Field
                label="Tare weight (kg)"
                name="tare"
                type="number"
                min="0"
                step="0.001"
              />
              <label className="flex gap-2">
                <input type="checkbox" name="serial" />
                Track individual serials
              </label>
            </>
          )}
          <Field
            label="Reorder level"
            name="reorder"
            type="number"
            min="0"
            step="0.001"
            defaultValue="0"
          />
          <Button disabled={busy || !units.length}>Create item</Button>
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
    <section className="panel grid gap-6" aria-label="Stock detail">
      <div className="flex justify-between gap-3">
        <div>
          <h2>{item.name}</h2>
          <p>
            {item.sku} · {item.kind} ·{' '}
            {item.serialTracked ? 'Serial tracked' : 'Quantity tracked'}
          </p>
        </div>
        {user.permissions.includes('catalog.manage') && (
          <Button variant="outline" onClick={onEdit}>
            Edit item
          </Button>
        )}
        <Button variant="outline" onClick={onClose}>
          Close detail
        </Button>
      </div>
      {error && (
        <p role="alert" className="field-error">
          {error}
        </p>
      )}
      <p>
        Stock is calculated from posted movements. Values and average costs are
        for the whole product at each warehouse, across all states.
      </p>
      {balances.isError ? (
        <PageState
          kind="error"
          title="Balances unavailable"
          description={balances.error.message}
          retry={() => void balances.refetch()}
        />
      ) : balances.isPending ? (
        <p role="status">Loading balances…</p>
      ) : !visible.length ? (
        <p>No stock movements in the active branch.</p>
      ) : (
        <div className="summary-grid">
          {visible.map((b) => (
            <div key={`${b.warehouseId}:${b.bucket}`} className="status-card">
              <h3>
                {warehouses.find((w) => w.id === b.warehouseId)?.name} ·{' '}
                {b.bucket}
              </h3>
              <p>Quantity: {b.quantity}</p>
              <p>Warehouse value: PKR {b.warehouseValue.toFixed(2)}</p>
              <p>Average unit cost: PKR {b.averageCost.toFixed(6)}</p>
            </div>
          ))}
        </div>
      )}
      <div className="flex flex-wrap gap-3 items-end">
        <Select
          label="Ledger warehouse"
          value={warehouseId}
          onChange={(v) => {
            setWarehouseId(v);
            setPagination((p) => ({ ...p, pageIndex: 0 }));
          }}
        >
          <option value="">Choose warehouse</option>
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
            Post stock movement
          </Button>
        )}
      </div>
      {warehouseId && (
        <DataTable
          title="Stock movement ledger"
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
                <Button
                  variant="ghost"
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
                  View document
                </Button>
              ),
            },
          ]}
        />
      )}
      {item.serialTracked && user.permissions.includes('catalog.manage') && (
        <details>
          <summary>Cylinder assets and inspection records</summary>
          {assets.error && <p role="alert">{assets.error.message}</p>}
          <ul>
            {assets.data?.map((a) => (
              <li key={a.id}>
                {a.serial} · {a.owner} · Inspection{' '}
                {a.inspectionReference || 'not recorded'}{' '}
                {a.inspectionDue || ''}
              </li>
            ))}
          </ul>
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
            <Field label="Cylinder serial" name="serial" required />
            <Field label="Cylinder owner" name="owner" required />
            <Field label="Inspection reference" name="inspection" />
            <Field label="Next inspection date" name="due" type="date" />
            <Button disabled={busy}>Register cylinder</Button>
          </form>
        </details>
      )}
      <Modal
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          if (!document) postTrigger.current?.focus({ preventScroll: true });
        }}
        title="Post stock movement"
        description="Posting is permanent. Corrections use a separate reversal. The server determines the business date and valuation."
        open={postOpen}
        onOpenChange={(v) => {
          if (!busy) setPostOpen(v);
        }}
      >
        {error && <p role="alert">{error}</p>}
        {!pending ? (
          <form
            className="grid gap-3"
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
            <p>From {warehouses.find((w) => w.id === warehouseId)?.name}</p>
            <Select label="Movement kind" value={kind} onChange={setKind}>
              {['Opening', 'AdjustmentIn', 'AdjustmentOut', 'Transfer'].map(
                (k) => (
                  <option key={k}>{k}</option>
                ),
              )}
            </Select>
            <Select label="Source state" name="bucket">
              {states(item).map((s) => (
                <option key={s}>{s}</option>
              ))}
            </Select>
            <Field
              label="Quantity"
              name="quantity"
              type="number"
              min="0.001"
              step="0.001"
              required
            />
            {(kind === 'Opening' || kind === 'AdjustmentIn') && (
              <Field
                label="Receipt unit cost (PKR)"
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
                  label="Destination warehouse"
                  value={destination}
                  onChange={setDestination}
                >
                  <option value="">Choose warehouse</option>
                  {allWarehouses
                    .filter((w) => w.active)
                    .map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.name}
                      </option>
                    ))}
                </Select>
                <Select label="Destination state" name="destinationBucket">
                  {states(item).map((s) => (
                    <option key={s}>{s}</option>
                  ))}
                </Select>
              </>
            )}
            {item.serialTracked && (
              <Field
                label="Serials (comma separated)"
                name="serials"
                required
              />
            )}
            <Field
              label="Posting reason / opening reconciliation reference"
              name="reason"
              required
              maxLength={300}
            />
            <Button>Review movement</Button>
          </form>
        ) : (
          <div className="grid gap-4">
            <p>
              {String(pending.kind)} · Quantity {String(pending.quantity)} ·{' '}
              {String(pending.bucket)}
            </p>
            <p>
              Source:{' '}
              {allWarehouses.find((w) => w.id === pending.warehouseId)?.name} ·{' '}
              {String(pending.bucket)}
            </p>
            {pending.destinationWarehouseId ? (
              <p>
                Destination:{' '}
                {
                  allWarehouses.find(
                    (w) => w.id === pending.destinationWarehouseId,
                  )?.name
                }{' '}
                · {String(pending.destinationBucket)}
              </p>
            ) : null}
            {pending.unitCost !== null && (
              <p>Receipt unit cost: PKR {String(pending.unitCost)}</p>
            )}
            <p>{String(pending.reason)}</p>
            <p>
              No totals are calculated in the browser. Retrying this submission
              uses the same command key.
            </p>
            <Button
              disabled={busy}
              onClick={() => void action('/documents', pending, true)}
            >
              Confirm posting
            </Button>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => setPending(null)}
            >
              Edit movement
            </Button>
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
        title="Stock document"
        description="Immutable movements and their original cost snapshots."
        open={!!document}
        onOpenChange={(open) => {
          if (!open) setDocument(null);
        }}
      >
        {error && <p role="alert">{error}</p>}
        {document && (
          <div className="grid gap-3">
            <p>
              {document.kind} · {document.reversedById ? 'Reversed' : 'Posted'}
            </p>
            <p>{document.id}</p>
            <p>
              {new Intl.DateTimeFormat(undefined, {
                dateStyle: 'medium',
                timeStyle: 'short',
                timeZone,
              }).format(new Date(document.postedAt))}{' '}
              · Business date {document.businessDate}
            </p>
            <p>{document.reason}</p>
            {document.movements.map((m) => (
              <p key={m.id}>
                {allWarehouses.find((w) => w.id === m.warehouseId)?.name} ·{' '}
                {m.bucket}: {m.quantity} · PKR {m.value.toFixed(2)}{' '}
                {m.serials.join(', ')}
              </p>
            ))}
            {!document.reversedById &&
              !document.reversesId &&
              user.permissions.includes('inventory.reverse') && (
                <form
                  className="grid gap-3"
                  onSubmit={(e) => e.preventDefault()}
                >
                  <Field
                    label="Reversal reason"
                    id="reversal-reason"
                    value={reverseReason}
                    onChange={(event) => setReverseReason(event.target.value)}
                    required
                    maxLength={300}
                  />
                  <ConfirmButton
                    destructive
                    disabled={busy || !reverseReason.trim()}
                    title="Reverse this stock document?"
                    description="An equal and opposite entry will be posted. Later dependent movements prevent reversal."
                    onConfirm={() => {
                      void action(
                        `/documents/${document.id}/reverse`,
                        { idempotencyKey: reverseKey, reason: reverseReason },
                        true,
                      );
                    }}
                  >
                    Reverse document
                  </ConfirmButton>
                </form>
              )}
          </div>
        )}
      </Modal>
    </section>
  );
}
