import { useId, useRef, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Field } from './components/ui/form';
import { Button } from './components/ui/button';
import type { Page } from './inventory-types';

type Account = {
  id: string;
  name: string;
  kind: string;
  defaultCash: boolean;
  balance: number;
};
type Category = { id: string; name: string; capital: boolean };
type Entry = {
  id: string;
  kind: string;
  amount: number;
  cash: number;
  accountId: string | null;
  customerId: string | null;
  supplierId: string | null;
  reason: string;
  businessDate: string;
  unapplied: number;
  counterparty: string;
  reversesId: string | null;
};
type Register = {
  status: string;
  opening: number;
  expected: number;
  lastEventId: string | null;
  history: {
    id: string;
    kind: string;
    counted: number;
    expected: number;
    variance: number;
    reason: string;
  }[];
};
type Credit = {
  balance: number;
  unapplied: number;
  invoices: {
    id: string;
    reference: string;
    date: string;
    original: number;
    outstanding: number;
    bucket: string;
  }[];
  entries: {
    id: string;
    kind: string;
    date: string;
    balanceChange: number;
    reason: string;
  }[];
};
type Party = { id: string; name: string };
const root = '/api/workspace/finance';
const money = (n: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);
const str = (d: FormData, key: string) => String(d.get(key) || '');
function Select({
  label,
  name,
  value,
  onChange,
  options,
  optional = false,
}: {
  label: string;
  name?: string;
  value?: string;
  onChange?: (s: string) => void;
  options: { id: string; name: string }[];
  optional?: boolean;
}) {
  const id = useId();
  return (
    <div className="grid gap-2">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        name={name}
        value={value}
        onChange={onChange ? (e) => onChange(e.target.value) : undefined}
        required={!optional}
      >
        <option value="">Select…</option>
        {options.map((x) => (
          <option key={x.id} value={x.id}>
            {x.name}
          </option>
        ))}
      </select>
    </div>
  );
}
export function FinancePage({
  user,
  section,
  timeZone,
}: {
  user: Me;
  section: string;
  timeZone: string;
}) {
  const branch = useBranch();
  return (
    <FinanceWorkspace
      key={`${user.tenantId}:${branch}:${section}`}
      user={user}
      branch={branch}
      section={section}
      timeZone={timeZone}
    />
  );
}
function FinanceWorkspace({
  user,
  branch,
  section,
  timeZone,
}: {
  user: Me;
  branch: string;
  section: string;
  timeZone: string;
}) {
  const cache = useQueryClient();
  const has = (p: string) => user.permissions.includes(p);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [page, setPage] = useState(1);
  const [account, setAccount] = useState('');
  const [date, setDate] = useState(
    new Date().toLocaleDateString('en-CA', { timeZone }),
  );
  const [supplier, setSupplier] = useState(false);
  const [party, setParty] = useState('');
  const [parent, setParent] = useState('');
  const kinds =
    section === 'expenses'
      ? ['Expense']
      : section === 'loans'
        ? [
            'LoanReceived',
            'LoanGiven',
            'LoanRepayment',
            'OwnerFunds',
            'OwnerWithdrawal',
          ]
        : section === 'udhaar'
          ? ['CustomerReceipt', 'SupplierPayment', 'Allocate']
          : [
              'Transfer',
              'OwnerFunds',
              'OwnerWithdrawal',
              ...(has('finance.manage')
                ? ['CashAdjustmentIn', 'CashAdjustmentOut']
                : []),
            ];
  const [kind, setKind] = useState(kinds[0]);
  const keys = useRef(new Map<string, string>());
  const get = <T,>(path: string) =>
    api<T>(root + path, 'GET', undefined, user.tenantId);
  const accounts = useQuery({
    queryKey: ['finance', user.tenantId, branch, 'accounts'],
    queryFn: () => get<Account[]>(`/accounts?branchId=${branch}`),
    enabled: !!branch,
  });
  const categories = useQuery({
    queryKey: ['finance', user.tenantId, 'categories'],
    queryFn: () => get<Category[]>('/categories'),
  });
  const entries = useQuery({
    queryKey: ['finance', user.tenantId, branch, page],
    queryFn: () => get<Page<Entry>>(`/entries?branchId=${branch}&page=${page}`),
    enabled: !!branch,
  });
  const openItems = useQuery({
    queryKey: ['finance', user.tenantId, branch, 'open-items'],
    queryFn: () => get<Entry[]>(`/open-items?branchId=${branch}`),
    enabled: !!branch,
  });
  const parties = useQuery({
    queryKey: ['finance', user.tenantId, supplier, 'parties'],
    queryFn: () => get<Party[]>(`/parties?supplier=${supplier}`),
  });
  const credit = useQuery({
    queryKey: ['finance', user.tenantId, branch, party, supplier, date],
    queryFn: () =>
      get<Credit>(
        `/credit?branchId=${branch}&partyId=${party}&supplier=${supplier}&asOf=${date}`,
      ),
    enabled: !!branch && !!party,
  });
  const register = useQuery({
    queryKey: ['finance', user.tenantId, account, date, 'register'],
    queryFn: () => get<Register>(`/register?accountId=${account}&date=${date}`),
    enabled:
      !!account &&
      accounts.data?.some((x) => x.id === account && x.kind === 'Cash'),
  });
  async function write(path: string, body: Record<string, unknown>) {
    if (busy) return;
    setBusy(true);
    setError('');
    setMessage('');
    const fingerprint = JSON.stringify({ path, body });
    const idempotencyKey = keys.current.get(fingerprint) || crypto.randomUUID();
    keys.current.set(fingerprint, idempotencyKey);
    try {
      await api(
        root + path,
        'POST',
        { ...body, idempotencyKey },
        user.tenantId,
      );
      keys.current.delete(fingerprint);
      await cache.invalidateQueries({ queryKey: ['finance'] });
      setMessage('Saved. Balances refreshed.');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function submit(
    event: FormEvent<HTMLFormElement>,
    fn: (d: FormData) => void,
  ) {
    event.preventDefault();
    fn(new FormData(event.currentTarget));
  }
  const common = (d: FormData) => ({
    branchId: branch,
    kind,
    amount: Number(str(d, 'amount')),
    reason: str(d, 'reason'),
    accountId: kind === 'Allocate' ? null : str(d, 'accountId') || null,
  });
  if (!branch) return <p>Select a branch to manage finances.</p>;
  const queryError =
    accounts.error ||
    entries.error ||
    categories.error ||
    openItems.error ||
    parties.error ||
    credit.error ||
    register.error;
  return (
    <div className="grid gap-6">
      {message && <p role="status">{message}</p>}
      {(error || queryError) && (
        <p role="alert">{error || queryError?.message}</p>
      )}
      {accounts.isPending && <p role="status">Loading accounts…</p>}
      <section className="panel grid gap-4">
        <h2>Cash and bank balances</h2>
        {accounts.data?.length === 0 && (
          <p>
            Create an account with a reconciled opening balance. The first cash
            account becomes the drawer used by sales, purchases and cylinder
            payments. Earlier transactions remain in their original history.
          </p>
        )}
        <ul>
          {accounts.data?.map((a) => (
            <li key={a.id}>
              {a.name} ({a.kind}
              {a.defaultCash ? ', default drawer' : ''}): {money(a.balance)}
            </li>
          ))}
        </ul>
        {has('finance.manage') && (
          <details>
            <summary>Create account</summary>
            <form
              className="grid gap-3"
              onSubmit={(e) =>
                submit(
                  e,
                  (d) =>
                    void write('/accounts', {
                      branchId: branch,
                      name: str(d, 'name'),
                      kind: str(d, 'kind'),
                      opening: Number(str(d, 'opening')),
                      reason: str(d, 'reason'),
                    }),
                )
              }
            >
              <Field
                label="Account name"
                name="name"
                required
                maxLength={120}
              />
              <Select
                label="Account type"
                name="kind"
                options={['Cash', 'Bank'].map((id) => ({ id, name: id }))}
              />
              <Field
                label="Reconciled opening balance (PKR)"
                name="opening"
                type="number"
                min="0"
                step="0.01"
                required
              />
              <Field
                label="Opening reconciliation reason"
                name="reason"
                required
                maxLength={300}
              />
              <Button disabled={busy}>Create account</Button>
            </form>
          </details>
        )}
      </section>
      {section === 'cash-register' && (
        <section className="panel grid gap-4">
          <h2>Drawer session</h2>
          <Select
            label="Cash drawer"
            value={account}
            onChange={setAccount}
            options={accounts.data?.filter((a) => a.kind === 'Cash') || []}
          />
          <Field
            label="Business date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          {register.data && (
            <>
              <p>
                Status: {register.data.status} · Opening:{' '}
                {money(register.data.opening)} · Expected:{' '}
                {money(register.data.expected)}
              </p>
              <p>
                Expected = opening + sales and collections + loans and owner
                funds + other cash in − purchases, expenses, supplier payments,
                repayments, withdrawals and bank deposits.
              </p>
              <form
                className="grid gap-3"
                onSubmit={(e) =>
                  submit(
                    e,
                    (d) =>
                      void write('/register', {
                        accountId: account,
                        businessDate: date,
                        kind:
                          register.data!.status === 'Not opened'
                            ? 'Open'
                            : register.data!.status === 'Closed'
                              ? 'Reopen'
                              : 'Close',
                        counted: Number(str(d, 'counted')),
                        reason: str(d, 'reason'),
                        expectedLastEventId: register.data!.lastEventId,
                      }),
                  )
                }
              >
                <Field
                  label="Counted cash (PKR)"
                  name="counted"
                  type="number"
                  step="0.01"
                  min="0"
                  required
                />
                <Field
                  label="Session / variance reason"
                  name="reason"
                  required
                  maxLength={300}
                />
                <Button
                  disabled={
                    busy ||
                    !has(
                      register.data.status === 'Closed'
                        ? 'register.reopen'
                        : register.data.status === 'Open'
                          ? 'register.close'
                          : 'finance.post',
                    )
                  }
                >
                  {register.data.status === 'Not opened'
                    ? 'Open register'
                    : register.data.status === 'Closed'
                      ? 'Reopen register'
                      : 'Close register'}
                </Button>
              </form>
              <ul>
                {register.data.history.map((h) => (
                  <li key={h.id}>
                    {h.kind}: expected {money(h.expected)}, counted{' '}
                    {money(h.counted)}, variance {money(h.variance)} —{' '}
                    {h.reason}
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>
      )}
      {section === 'udhaar' && (
        <section className="panel grid gap-4">
          <h2>Udhaar and aging</h2>
          <Select
            label="Ledger type"
            value={supplier ? 'supplier' : 'customer'}
            onChange={(s) => {
              setSupplier(s === 'supplier');
              setParty('');
            }}
            options={[
              { id: 'customer', name: 'Customer' },
              { id: 'supplier', name: 'Supplier' },
            ]}
          />
          <Select
            label="Counterparty"
            value={party}
            onChange={setParty}
            options={parties.data || []}
          />
          <Field
            label="As of date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          {credit.data && (
            <>
              <p>
                Balance: {money(credit.data.balance)} · Unapplied:{' '}
                {money(credit.data.unapplied)}
              </p>
              <p>
                Aging uses invoice dates. Refundable cylinder deposits are
                separate from receivables.
              </p>
              <div className="overflow-x-auto">
                <table>
                  <caption>Outstanding invoices</caption>
                  <thead>
                    <tr>
                      <th>Reference</th>
                      <th>Date</th>
                      <th>Outstanding</th>
                      <th>Age</th>
                    </tr>
                  </thead>
                  <tbody>
                    {credit.data.invoices.map((i) => (
                      <tr key={i.id}>
                        <td>{i.reference}</td>
                        <td>{i.date}</td>
                        <td>{money(i.outstanding)}</td>
                        <td>{i.bucket}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <details>
                <summary>Append-only credit history</summary>
                <ul>
                  {credit.data.entries.map((e) => (
                    <li key={e.id}>
                      {e.date} · {e.kind} · {money(e.balanceChange)} ·{' '}
                      {e.reason}
                    </li>
                  ))}
                </ul>
              </details>
            </>
          )}
        </section>
      )}
      {section === 'expenses' && has('finance.manage') && (
        <section className="panel">
          <h2>Expense categories</h2>
          <ul>
            {categories.data?.map((c) => (
              <li key={c.id}>
                {c.name} ({c.capital ? 'Capital item' : 'Operating expense'})
              </li>
            ))}
          </ul>
          <form
            className="grid gap-3"
            onSubmit={(e) =>
              submit(
                e,
                (d) =>
                  void write('/categories', {
                    name: str(d, 'name'),
                    capital: d.has('capital'),
                  }),
              )
            }
          >
            <Field label="Category name" name="name" required maxLength={120} />
            <Field
              label="Capital item (excluded from operating expenses)"
              name="capital"
              type="checkbox"
            />
            <Button disabled={busy}>Add category</Button>
          </form>
        </section>
      )}
      {has('finance.post') && (
        <section className="panel grid gap-4">
          <h2>Record transaction</h2>
          <form
            className="grid gap-3"
            onSubmit={(e) =>
              submit(e, (d) => {
                const p = openItems.data?.find((x) => x.id === parent);
                const partyFields =
                  kind === 'CustomerReceipt'
                    ? { customerId: party }
                    : kind === 'SupplierPayment'
                      ? { supplierId: party }
                      : {};
                void write('/entries', {
                  ...common(d),
                  ...partyFields,
                  ...(kind === 'Expense'
                    ? { categoryId: str(d, 'categoryId') }
                    : {}),
                  ...(kind === 'Transfer'
                    ? { destinationAccountId: str(d, 'destination') }
                    : {}),
                  ...(kind === 'LoanReceived' || kind === 'LoanGiven'
                    ? { counterparty: str(d, 'counterparty') }
                    : {}),
                  ...(kind === 'Allocate' || kind === 'LoanRepayment'
                    ? { parentId: parent }
                    : {}),
                  ...(kind === 'Allocate'
                    ? p?.customerId
                      ? { saleId: str(d, 'invoiceId') }
                      : { purchaseId: str(d, 'invoiceId') }
                    : {}),
                });
              })
            }
          >
            <Select
              label="Transaction type"
              value={kind}
              onChange={(s) => {
                setKind(s);
                setParent('');
                if (s === 'CustomerReceipt' || s === 'SupplierPayment') {
                  setSupplier(s === 'SupplierPayment');
                  setParty('');
                }
              }}
              options={kinds.map((id) => ({
                id,
                name: id.replace(/([a-z])([A-Z])/g, '$1 $2'),
              }))}
            />
            {kind !== 'Allocate' && (
              <Select
                label="Payment account"
                name="accountId"
                options={accounts.data || []}
              />
            )}
            {kind === 'Expense' && (
              <Select
                label="Expense category"
                name="categoryId"
                options={categories.data || []}
              />
            )}
            {kind === 'Transfer' && (
              <Select
                label="Destination account"
                name="destination"
                options={accounts.data || []}
              />
            )}
            {(kind === 'CustomerReceipt' || kind === 'SupplierPayment') && (
              <p>
                Payment will be recorded for the selected{' '}
                {kind === 'CustomerReceipt' ? 'customer' : 'supplier'} above.
                Leave it unapplied or allocate it to invoices afterwards.
              </p>
            )}
            {(kind === 'LoanReceived' || kind === 'LoanGiven') && (
              <Field
                label="Loan counterparty"
                name="counterparty"
                required
                maxLength={120}
              />
            )}
            {(kind === 'Allocate' || kind === 'LoanRepayment') && (
              <Select
                label={
                  kind === 'Allocate' ? 'Unapplied payment' : 'Outstanding loan'
                }
                value={parent}
                onChange={(id) => {
                  setParent(id);
                  const p = openItems.data?.find((x) => x.id === id);
                  if (kind === 'Allocate') {
                    setSupplier(!!p?.supplierId);
                    setParty(p?.customerId || p?.supplierId || '');
                  }
                }}
                options={(openItems.data || [])
                  .filter((x) =>
                    kind === 'Allocate'
                      ? ['CustomerReceipt', 'SupplierPayment'].includes(x.kind)
                      : ['LoanReceived', 'LoanGiven'].includes(x.kind),
                  )
                  .map((x) => ({
                    id: x.id,
                    name: `${x.counterparty || x.kind} · ${x.reason} · ${money(x.unapplied)}`,
                  }))}
              />
            )}
            {kind === 'Allocate' && (
              <Select
                label="Invoice to allocate"
                name="invoiceId"
                options={(credit.data?.invoices || [])
                  .filter(
                    (x) =>
                      x.outstanding > 0 &&
                      x.reference !== 'Cylinder settlement',
                  )
                  .map((x) => ({
                    id: x.id,
                    name: `${x.reference} · ${money(x.outstanding)}`,
                  }))}
              />
            )}
            <Field
              label="Amount (PKR)"
              name="amount"
              type="number"
              min="0.01"
              step="0.01"
              required
            />
            <Field
              label="Transaction reason / reference"
              name="reason"
              required
              maxLength={300}
            />
            <Button
              disabled={
                busy ||
                ((kind === 'CustomerReceipt' || kind === 'SupplierPayment') &&
                  !party)
              }
            >
              Post transaction
            </Button>
          </form>
        </section>
      )}
      <section className="panel grid gap-4">
        <h2>Financial history</h2>
        {entries.data?.items.length === 0 && <p>No financial entries yet.</p>}
        <div className="overflow-x-auto">
          <table>
            <caption>Posted entries — page {page}</caption>
            <thead>
              <tr>
                <th>Date</th>
                <th>Type / reason</th>
                <th>Account movement</th>
                <th>Unapplied / principal</th>
                <th>Correction</th>
              </tr>
            </thead>
            <tbody>
              {entries.data?.items.map((e) => (
                <tr key={e.id}>
                  <td>{e.businessDate}</td>
                  <td>
                    {e.kind}
                    <br />
                    {e.reason}
                  </td>
                  <td>{money(e.cash)}</td>
                  <td>{money(e.unapplied)}</td>
                  <td>
                    {has('finance.reverse') &&
                      !e.reversesId &&
                      ![
                        'Opening',
                        'SaleCash',
                        'PurchaseCash',
                        'CylinderCash',
                        'TransferIn',
                      ].includes(e.kind) && (
                        <form
                          onSubmit={(event) =>
                            submit(
                              event,
                              (d) =>
                                void write(`/entries/${e.id}/reverse`, {
                                  reason: str(d, 'reason'),
                                }),
                            )
                          }
                        >
                          <Field
                            label={`Reversal reason for ${e.reason}`}
                            name="reason"
                            required
                            maxLength={300}
                          />
                          <Button disabled={busy} variant="secondary">
                            Reverse entry
                          </Button>
                        </form>
                      )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex gap-3">
          <Button
            variant="secondary"
            disabled={page === 1}
            onClick={() => setPage(page - 1)}
          >
            Previous
          </Button>
          <Button
            variant="secondary"
            disabled={!entries.data || page * 20 >= entries.data.total}
            onClick={() => setPage(page + 1)}
          >
            Next
          </Button>
        </div>
      </section>
    </div>
  );
}
