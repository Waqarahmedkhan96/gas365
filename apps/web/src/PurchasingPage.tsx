import { useId, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Button } from './components/ui/button';
import { Field } from './components/ui/form';
import { DataTable } from './components/DataTable';
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

export function PurchasingPage({
  user,
  suppliersOnly,
}: {
  user: Me;
  suppliersOnly: boolean;
}) {
  const branch = useBranch();
  return (
    <PurchasingWorkspace
      key={`${user.tenantId}:${branch}:${suppliersOnly}`}
      user={user}
      branch={branch}
      suppliersOnly={suppliersOnly}
    />
  );
}
function PurchasingWorkspace({
  user,
  branch,
  suppliersOnly,
}: {
  user: Me;
  branch: string;
  suppliersOnly: boolean;
}) {
  const cache = useQueryClient();
  const has = (p: string) => user.permissions.includes(p);
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [search, setSearch] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [purchaseId, setPurchaseId] = useState('');
  const [editor, setEditor] = useState<Purchase | 'new' | null>(null);
  const [supplierEdit, setSupplierEdit] = useState<Supplier | 'new' | null>(
    null,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [ledgerPage, setLedgerPage] = useState(1);
  const keys = useRef(new Map<string, string>());
  const get = <T,>(path: string) =>
    api<T>(root + path, 'GET', undefined, user.tenantId);
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
  const detail = useQuery({
    queryKey: ['purchasing', user.tenantId, 'detail', purchaseId],
    enabled: !!purchaseId,
    queryFn: () => get<Purchase>(`/purchases/${purchaseId}`),
  });
  const profile = useQuery({
    queryKey: ['purchasing', user.tenantId, branch, supplierId, ledgerPage],
    enabled: !!supplierId && !!branch,
    queryFn: () =>
      get<SupplierProfile>(
        `/suppliers/${supplierId}?branchId=${branch}&page=${ledgerPage}`,
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
    setPurchaseId(result.id);
  }
  const p = detail.data;
  return (
    <div className="grid gap-6">
      {error && (
        <p role="alert">
          {error} For an uncertain response, retry without changing the command.
        </p>
      )}
      {notice && <p role="status">{notice}</p>}
      {!branch && (
        <p role="status">
          Choose a branch to work with purchases and supplier balances.
        </p>
      )}
      {suppliersOnly ? (
        <>
          <div className="flex flex-wrap gap-3">
            <Field
              label="Search suppliers"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPagination({ pageIndex: 0, pageSize: 20 });
              }}
            />
            {has('suppliers.manage') && (
              <Button onClick={() => setSupplierEdit('new')}>
                Add supplier
              </Button>
            )}
          </div>
          <DataTable
            title="Suppliers"
            data={suppliers.data?.items || []}
            rowCount={suppliers.data?.total || 0}
            pagination={pagination}
            onPaginationChange={setPagination}
            loading={suppliers.isPending}
            error={suppliers.error?.message}
            retry={() => void suppliers.refetch()}
            columns={[
              {
                header: 'Supplier',
                accessorKey: 'name',
                cell: ({ row }) => (
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setSupplierId(row.original.id);
                      setLedgerPage(1);
                    }}
                  >
                    {row.original.name}
                  </Button>
                ),
              },
              { header: 'Phone', accessorKey: 'phone' },
              {
                header: 'Status',
                cell: ({ row }) =>
                  row.original.active ? 'Active' : 'Inactive',
              },
            ]}
          />
          {supplierEdit && (
            <form
              key={
                supplierEdit === 'new'
                  ? 'new'
                  : `${supplierEdit.id}:${supplierEdit.version}`
              }
              className="status-card grid gap-3"
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
                      version:
                        supplierEdit === 'new' ? 0 : supplierEdit.version,
                    },
                    user.tenantId,
                  );
                  setSupplierEdit(null);
                });
              }}
            >
              <h2>
                {supplierEdit === 'new' ? 'New supplier' : 'Edit supplier'}
              </h2>
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
                defaultValue={supplierEdit === 'new' ? '' : supplierEdit.phone}
              />
              <Field
                label="Address"
                name="address"
                maxLength={300}
                defaultValue={
                  supplierEdit === 'new' ? '' : supplierEdit.address
                }
              />
              <Field
                label="Active supplier"
                name="active"
                type="checkbox"
                defaultChecked={supplierEdit === 'new' || supplierEdit.active}
              />
              <Button disabled={busy}>Save supplier</Button>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setSupplierEdit(null)}
              >
                Cancel
              </Button>
            </form>
          )}
          {profile.error && <p role="alert">{profile.error.message}</p>}
          {profile.data && (
            <section className="status-card grid gap-3">
              <h2>{profile.data.supplier.name}</h2>
              <p>
                {profile.data.supplier.phone} {profile.data.supplier.address}
              </p>
              <p>Payable in selected branch: {money(profile.data.balance)}</p>
              {has('suppliers.manage') && (
                <Button onClick={() => setSupplierEdit(profile.data!.supplier)}>
                  Edit supplier
                </Button>
              )}
              <h3>Supplier ledger</h3>
              {profile.data.ledger.items.map(({ purchaseId: id, entry }) => (
                <div key={entry.id} className="border-b py-2">
                  <Button variant="ghost" onClick={() => setPurchaseId(id)}>
                    Open purchase {id.slice(0, 8)}
                  </Button>
                  <p>
                    {entry.businessDate} · {entry.kind} · Payable{' '}
                    {money(entry.payable)} · Cash {money(entry.cash)}
                  </p>
                  <p>{entry.reason}</p>
                </div>
              ))}
              <div className="flex gap-2">
                <Button
                  disabled={ledgerPage === 1}
                  onClick={() => setLedgerPage((x) => x - 1)}
                >
                  Previous ledger page
                </Button>
                <Button
                  disabled={ledgerPage * 20 >= profile.data.ledger.total}
                  onClick={() => setLedgerPage((x) => x + 1)}
                >
                  Next ledger page
                </Button>
              </div>
            </section>
          )}
        </>
      ) : (
        <>
          {has('purchases.create') && (
            <Button disabled={!branch} onClick={() => setEditor('new')}>
              New purchase
            </Button>
          )}
          <DataTable
            title="Purchases"
            data={purchases.data?.items || []}
            rowCount={purchases.data?.total || 0}
            pagination={pagination}
            onPaginationChange={setPagination}
            loading={purchases.isPending && !!branch}
            error={purchases.error?.message}
            retry={() => void purchases.refetch()}
            columns={[
              {
                header: 'Reference',
                cell: ({ row }) => (
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setPurchaseId(row.original.id);
                      setEditor(null);
                    }}
                  >
                    {row.original.snapshot.reference}
                  </Button>
                ),
              },
              { header: 'Supplier', accessorKey: 'supplierName' },
              { header: 'Status', accessorKey: 'status' },
              { header: 'Total', cell: ({ row }) => money(row.original.total) },
              {
                header: 'Outstanding',
                cell: ({ row }) => money(row.original.outstanding),
              },
            ]}
          />
        </>
      )}
      {editor && (
        <DraftEditor
          key={editor === 'new' ? 'new' : editor.id}
          initial={editor === 'new' ? undefined : editor}
          user={user}
          branch={branch}
          busy={busy}
          cancel={() => setEditor(null)}
          save={(body, id) =>
            void run(async () => {
              const d = await api<Purchase>(
                root + '/purchases' + (id ? '/' + id : ''),
                id ? 'PUT' : 'POST',
                body,
                user.tenantId,
              );
              setPurchaseId(d.id);
              setEditor(null);
            })
          }
        />
      )}
      {detail.isFetching && purchaseId && (
        <p role="status">Loading purchase…</p>
      )}
      {detail.error && <p role="alert">{detail.error.message}</p>}
      {p && (
        <section
          className="status-card grid gap-4"
          aria-label="Purchase details"
        >
          <h2>Purchase {p.snapshot.reference}</h2>
          <p>
            {p.supplierName} · <strong>{p.status}</strong>
          </p>
          <p>
            Allocation: {p.snapshot.allocationRule} · Landed charges:{' '}
            {money(p.snapshot.landedCharges)}
          </p>
          <p>{p.snapshot.notes}</p>
          {p.correctsId && <p>Corrects purchase {p.correctsId}</p>}
          {p.snapshot.lines.map((line, i) => (
            <div key={i} className="border-b py-3">
              <h3>
                {line.sku} · {line.name}
              </h3>
              <p>
                {line.quantity} {line.unit} · {line.warehouse} · {line.bucket}
              </p>
              <p>
                Base {money(line.baseValue)} + landed {money(line.landedCost)} ={' '}
                {money(line.value)}
              </p>
              <p>Receipt unit cost: {line.receiptUnitCost} PKR</p>
              {line.serials.length > 0 && (
                <p>Serials: {line.serials.join(', ')}</p>
              )}
            </div>
          ))}
          <p>
            Total: {money(p.total)} · Paid: {money(p.paid)} · Outstanding:{' '}
            {money(p.outstanding)}
          </p>
          <Button variant="secondary" onClick={() => void detail.refetch()}>
            Refresh purchase
          </Button>
          {p.status === 'Draft' ? (
            <>
              {has('purchases.create') && (
                <Button onClick={() => setEditor(p)}>Edit draft</Button>
              )}
              {has('purchases.post') && (
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
                  <Button disabled={busy}>Post purchase</Button>
                </form>
              )}
            </>
          ) : (
            <>
              <Button
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
                Download PDF voucher
              </Button>
              {p.status !== 'Reversed' &&
                p.outstanding > 0 &&
                has('purchases.pay') && (
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
                    <h3>Record cash payment</h3>
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
                    <Button disabled={busy}>Record payment</Button>
                  </form>
                )}
              {p.separateCosts && (
                <div className="grid gap-3">
                  <h3>Separately paid purchase costs</h3>
                  <p>
                    Petrol, tolls, loading and unloading increase stock cost.
                    They are paid separately from the goods supplier.
                  </p>
                  <p>
                    Costs paid: {money(p.costsPaid)} · Costs unpaid:{' '}
                    {money(p.costsOutstanding)}
                  </p>
                  {p.status !== 'Reversed' &&
                    p.costsOutstanding > 0 &&
                    has('purchases.pay') && (
                      <form
                        className="grid gap-3"
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
                        <Button disabled={busy}>Record cost payment</Button>
                      </form>
                    )}
                </div>
              )}
              <h3>Payment and correction history</h3>
              {p.entries.map((entry) => (
                <div key={entry.id} className="border-b py-3">
                  <p>
                    {entry.businessDate} · {entry.kind} · Payable{' '}
                    {money(entry.payable)} · Cash {money(entry.cash)}
                  </p>
                  <p>{entry.reason}</p>
                  {(entry.kind === 'Payment' || entry.kind === 'CostPayment') &&
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
                </div>
              ))}
              {p.status !== 'Reversed' && has('purchases.reverse') && (
                <ReasonForm
                  label="Reverse purchase"
                  busy={busy}
                  action={(reason) =>
                    void run(() =>
                      command(`/purchases/${p.id}/reverse`, { reason }),
                    )
                  }
                />
              )}
              {p.status === 'Reversed' && has('purchases.create') && (
                <Button
                  onClick={() =>
                    setEditor({ ...p, id: '', version: 0, correctsId: p.id })
                  }
                >
                  Create replacement draft
                </Button>
              )}
            </>
          )}
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
      className="grid gap-3"
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
  return (
    <form
      className="status-card grid gap-4"
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
      <h2>{initial?.id ? 'Edit purchase draft' : 'New purchase draft'}</h2>
      {[items.error, suppliers.error, warehouses.error]
        .filter(Boolean)
        .map((e, i) => (
          <p key={i} role="alert">
            {e!.message}
          </p>
        ))}
      <Field
        label="Find supplier"
        value={supplierSearch}
        onChange={(e) => setSupplierSearch(e.target.value)}
      />
      <Select
        label="Supplier"
        value={supplier}
        onChange={(e) => setSupplier(e.target.value)}
      >
        <option value="">Choose supplier</option>
        {initial &&
          !suppliers.data?.items.some((s) => s.id === initial.supplierId) && (
            <option value={initial.supplierId}>{initial.supplierName}</option>
          )}
        {suppliers.data?.items
          .filter((s) => s.active)
          .map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
      </Select>
      <Field
        label="Supplier reference"
        name="reference"
        required
        maxLength={120}
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
      <p>
        Charges increase stock cost and are paid separately from the goods
        supplier. Weight uses each line's total received kilograms. Save the
        draft to see server-calculated amounts.
      </p>
      <Field
        label="Find catalog item"
        value={itemSearch}
        onChange={(e) => setItemSearch(e.target.value)}
      />
      {lines.map((line, i) => (
        <fieldset key={i} className="grid gap-3 rounded-lg border p-4">
          <legend>Line {i + 1}</legend>
          <Select
            label={`Item ${i + 1}`}
            value={line.itemId}
            onChange={(e) => {
              const item = items.data?.items.find(
                (x) => x.id === e.target.value,
              );
              update(i, {
                itemId: e.target.value,
                bucket: item?.kind === 'Cylinder' ? 'Empty' : 'Available',
              });
            }}
          >
            <option value="">Choose item</option>
            {line.itemId &&
              !items.data?.items.some((x) => x.id === line.itemId) && (
                <option value={line.itemId}>
                  {initial?.snapshot.lines.find((x) => x.itemId === line.itemId)
                    ?.name || line.itemId}
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
            onChange={(e) => update(i, { warehouseId: e.target.value })}
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
          <Button
            type="button"
            variant="secondary"
            disabled={lines.length === 1}
            onClick={() => setLines((old) => old.filter((_, j) => i !== j))}
          >
            Remove line {i + 1}
          </Button>
        </fieldset>
      ))}
      <Button
        type="button"
        variant="secondary"
        disabled={lines.length >= 100}
        onClick={() => setLines((old) => [...old, blank()])}
      >
        Add line
      </Button>
      <Button
        disabled={
          busy || items.isPending || suppliers.isPending || warehouses.isPending
        }
      >
        Save draft
      </Button>
      <Button type="button" variant="secondary" onClick={cancel}>
        Cancel draft editing
      </Button>
    </form>
  );
}
