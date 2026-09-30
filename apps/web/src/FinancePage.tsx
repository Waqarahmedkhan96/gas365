import { useId, useRef, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import { Field } from './components/ui/form';
import { Button } from './components/ui/button';
import {
  Calculator,
  CheckCircle2,
  Coins,
  DoorOpen,
  HandCoins,
  Landmark,
  Tags,
  Wallet,
} from 'lucide-react';
import {
  Card,
  PageState,
  StatCard,
  StatGrid,
  StatusChip,
} from './components/page';
import type { Page } from './inventory-types';
import { tr } from './i18n';

type Account = {
  id: string;
  name: string;
  kind: string;
  defaultCash: boolean;
  balance: number;
  active: boolean;
  version: number;
};
type Category = {
  id: string;
  name: string;
  capital: boolean;
  active: boolean;
  version: number;
};
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
    kind: 'Sale' | 'Purchase' | 'CylinderLoan' | 'Opening';
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
// Server kinds are PascalCase words; show them as plain words.
const label = (kind: string) => tr(kind.replace(/([a-z])([A-Z])/g, '$1 $2'));
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
        <option value="">{tr('Select…')}</option>
        {options.map((x) => (
          <option key={x.id} value={x.id}>
            {x.name}
          </option>
        ))}
      </select>
    </div>
  );
}
// Renames or retires account/category master data. History keeps its original references.
function MasterEditor({
  label,
  name,
  active,
  busy,
  onSave,
}: {
  label: string;
  name: string;
  active: boolean;
  busy: boolean;
  onSave: (name: string, active: boolean) => void;
}) {
  return (
    <details className="row-action">
      <summary>
        {tr('Edit')} {label}
      </summary>
      <form
        className="grid gap-3"
        onSubmit={(e) => {
          e.preventDefault();
          const d = new FormData(e.currentTarget);
          onSave(str(d, 'name'), d.has('active'));
        }}
      >
        <Field
          label={`Name for ${label}`}
          name="name"
          defaultValue={name}
          required
          maxLength={120}
        />
        <Field
          label={`${label} is active`}
          name="active"
          type="checkbox"
          defaultChecked={active}
        />
        <Button disabled={busy} variant="outline">
          {tr('Save')} {label}
        </Button>
      </form>
    </details>
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
  async function run(action: () => Promise<unknown>) {
    if (busy) return;
    setBusy(true);
    setError('');
    setMessage('');
    try {
      await action();
      await cache.invalidateQueries({ queryKey: ['finance'] });
      setMessage('Saved. Balances refreshed.');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function write(path: string, body: Record<string, unknown>) {
    const fingerprint = JSON.stringify({ path, body });
    const idempotencyKey = keys.current.get(fingerprint) || crypto.randomUUID();
    keys.current.set(fingerprint, idempotencyKey);
    return run(async () => {
      await api(
        root + path,
        'POST',
        { ...body, idempotencyKey },
        user.tenantId,
      );
      keys.current.delete(fingerprint);
    });
  }
  const update = (path: string, body: Record<string, unknown>) =>
    run(() => api(root + path, 'PUT', body, user.tenantId));
  const usableAccounts = accounts.data?.filter((a) => a.active) || [];
  const usableCategories = categories.data?.filter((c) => c.active) || [];
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
  // The server validates that the target belongs to the payment's counterparty and branch.
  function allocationTarget(id: string) {
    const line = credit.data?.invoices.find((x) => x.id === id);
    return line?.kind === 'CylinderLoan'
      ? { cylinderLoanId: id }
      : line?.kind === 'Opening'
        ? { openingBalanceId: id }
        : line?.kind === 'Sale'
          ? { saleId: id }
          : { purchaseId: id };
  }
  if (!branch)
    return (
      <PageState
        kind="empty"
        title={tr('Choose a branch')}
        description={tr('Select a branch in the top bar to manage finances.')}
      />
    );
  const queryError =
    accounts.error ||
    entries.error ||
    categories.error ||
    openItems.error ||
    parties.error ||
    credit.error ||
    register.error;
  const registerAction =
    register.data?.status === 'Not opened'
      ? 'Open register'
      : register.data?.status === 'Closed'
        ? 'Reopen register'
        : 'Close register';
  const registerTone =
    register.data?.status === 'Open'
      ? 'success'
      : register.data?.status === 'Closed'
        ? 'info'
        : 'warning';
  const history = (
    <Card title={tr('Financial history')}>
      {entries.data?.items.length === 0 ? (
        <p className="muted-text">{tr('No financial entries yet.')}</p>
      ) : (
        <div
          className="data-table finance-table"
          role="region"
          aria-label={tr('Financial history table')}
          tabIndex={0}
        >
          <table>
            <caption>
              {tr('Posted entries — page')} {page}
            </caption>
            <thead>
              <tr>
                <th scope="col">{tr('Date')}</th>
                <th scope="col">{tr('Type / reason')}</th>
                <th scope="col">{tr('Account movement')}</th>
                <th scope="col">{tr('Unapplied / principal')}</th>
                <th scope="col">{tr('Correction')}</th>
              </tr>
            </thead>
            <tbody>
              {entries.data?.items.map((e) => (
                <tr key={e.id}>
                  <td className="whitespace-nowrap">{e.businessDate}</td>
                  <td>
                    <span className="kind-tag">{label(e.kind)}</span>
                    <span className="cell-note">{e.reason}</span>
                  </td>
                  <td
                    className={
                      e.cash < 0
                        ? 'amount-out whitespace-nowrap'
                        : 'amount-in whitespace-nowrap'
                    }
                  >
                    {money(e.cash)}
                  </td>
                  <td className="whitespace-nowrap">{money(e.unapplied)}</td>
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
                        <details className="row-action">
                          <summary>{tr('Reverse')}</summary>
                          <form
                            className="inline-form"
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
                            <Button disabled={busy} variant="outline">
                              {tr('Reverse entry')}
                            </Button>
                          </form>
                        </details>
                      )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="flex gap-3 pt-4">
        <Button
          variant="outline"
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          {tr('Previous')}
        </Button>
        <Button
          variant="outline"
          disabled={!entries.data || page * 20 >= entries.data.total}
          onClick={() => setPage(page + 1)}
        >
          {tr('Next')}
        </Button>
      </div>
    </Card>
  );
  return (
    <div className="grid gap-6">
      {message && (
        <p role="status" className="notice-banner">
          <CheckCircle2 aria-hidden="true" size={18} /> {message}
        </p>
      )}
      {(error || queryError) && (
        <p role="alert" className="error-banner">
          {error || queryError?.message}
        </p>
      )}
      {accounts.isPending && <p role="status">{tr('Loading accounts…')}</p>}
      {!!accounts.data?.length && (
        <StatGrid label={tr('Cash and bank balances')}>
          {accounts.data.map((a) => (
            <StatCard
              key={a.id}
              label={a.name}
              icon={a.kind === 'Cash' ? Wallet : Landmark}
              tone={a.active ? 'neutral' : 'warning'}
              value={money(a.balance)}
              note={`${a.kind}${a.defaultCash ? ' · default drawer' : ''}${a.active ? '' : ' · retired'}`}
            />
          ))}
        </StatGrid>
      )}
      <div className="split-layout">
        <div className="grid gap-6 content-start">
          {section === 'cash-register' && (
            <Card title={tr('Drawer session')} icon={Wallet}>
              <div className="form-grid">
                <Select
                  label={tr('Cash drawer')}
                  value={account}
                  onChange={setAccount}
                  options={usableAccounts.filter((a) => a.kind === 'Cash')}
                />
                <Field
                  label={tr('Business date')}
                  type="date"
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                />
              </div>
              {!account && (
                <p className="muted-text pt-4">
                  {tr(
                    'Choose a cash drawer to open, count or close it for the day.',
                  )}
                </p>
              )}
              {register.data && (
                <div className="grid gap-4 pt-4">
                  {register.data.status === 'Not opened' && (
                    <div className="register-callout">
                      <DoorOpen aria-hidden="true" size={28} />
                      <div>
                        <strong>
                          {tr('The register is not open for {date}', { date })}
                        </strong>
                        <p>
                          {tr(
                            'Count the cash in the drawer and open the register before recording sales and payments.',
                          )}
                        </p>
                      </div>
                    </div>
                  )}
                  <StatGrid label={tr('Drawer session figures')}>
                    <StatCard
                      label={tr('Status')}
                      value={
                        <StatusChip tone={registerTone}>
                          {register.data.status}
                        </StatusChip>
                      }
                    />
                    <StatCard
                      label={tr('Opening')}
                      icon={Coins}
                      value={money(register.data.opening)}
                    />
                    <StatCard
                      label={tr('Expected')}
                      icon={Calculator}
                      value={money(register.data.expected)}
                      note={tr('Calculated by the server')}
                    />
                  </StatGrid>
                  <p className="section-help">
                    {tr(
                      'Expected = opening + sales and collections + loans and owner funds + other cash in − purchases, expenses, supplier payments, repayments, withdrawals and bank deposits.',
                    )}
                  </p>
                  <form
                    className="register-form"
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
                    <div className="form-grid">
                      <Field
                        label={tr('Counted cash (PKR)')}
                        name="counted"
                        type="number"
                        step="0.01"
                        min="0"
                        required
                      />
                      <Field
                        label={tr('Session / variance reason')}
                        name="reason"
                        required
                        maxLength={300}
                      />
                    </div>
                    <div>
                      <Button
                        variant={
                          register.data.status === 'Open'
                            ? 'destructive'
                            : 'default'
                        }
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
                        {registerAction}
                      </Button>
                    </div>
                  </form>
                  {register.data.history.length > 0 && (
                    <div className="grid gap-3">
                      <h4 className="subheading">{tr('Session history')}</h4>
                      <ol className="timeline">
                        {register.data.history.map((h) => (
                          <li key={h.id}>
                            <div className="timeline-head">
                              <strong>{label(h.kind)}</strong>
                              <span
                                className={
                                  h.variance < 0
                                    ? 'amount-out'
                                    : h.variance > 0
                                      ? 'amount-in'
                                      : undefined
                                }
                              >
                                {tr('Variance')} {money(h.variance)}
                              </span>
                            </div>
                            <p>
                              {tr('Expected')} {money(h.expected)}{' '}
                              {tr('· counted')} {money(h.counted)}
                            </p>
                            <p>{h.reason}</p>
                          </li>
                        ))}
                      </ol>
                    </div>
                  )}
                </div>
              )}
            </Card>
          )}
          {section === 'udhaar' && (
            <Card title={tr('Udhaar and aging')} icon={HandCoins}>
              <div className="form-grid">
                <Select
                  label={tr('Ledger type')}
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
                  label={tr('Counterparty')}
                  value={party}
                  onChange={setParty}
                  options={parties.data || []}
                />
                <Field
                  label={tr('As of date')}
                  type="date"
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                />
              </div>
              {!party && (
                <p className="muted-text pt-4">
                  {tr(
                    'Choose a customer or supplier to see their balance, open invoices and aging.',
                  )}
                </p>
              )}
              {credit.data && (
                <div className="grid gap-4 pt-4">
                  <StatGrid label={tr('Credit balance')}>
                    <StatCard
                      label={tr('Balance')}
                      icon={HandCoins}
                      tone={credit.data.balance > 0 ? 'warning' : 'success'}
                      value={money(credit.data.balance)}
                      note={tr('Net of unapplied payments')}
                    />
                    <StatCard
                      label={tr('Unapplied')}
                      icon={Wallet}
                      value={money(credit.data.unapplied)}
                      note={tr('Payments not yet allocated')}
                    />
                  </StatGrid>
                  <p className="section-help">
                    {tr(
                      'Aging uses invoice dates; opening balances age from their original invoice date. Refundable cylinder deposits are separate from receivables.',
                    )}
                  </p>
                  <div
                    className="data-table finance-table"
                    role="region"
                    aria-label={tr('Outstanding invoices table')}
                    tabIndex={0}
                  >
                    <table>
                      <caption>{tr('Outstanding invoices')}</caption>
                      <thead>
                        <tr>
                          <th scope="col">{tr('Reference')}</th>
                          <th scope="col">{tr('Type')}</th>
                          <th scope="col">{tr('Date')}</th>
                          <th scope="col">{tr('Outstanding')}</th>
                          <th scope="col">{tr('Age')}</th>
                          <th scope="col">{tr('Correction')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {credit.data.invoices.map((i) => (
                          <tr key={i.id}>
                            <td>{i.reference}</td>
                            <td>{label(i.kind)}</td>
                            <td className="whitespace-nowrap">{i.date}</td>
                            <td className="whitespace-nowrap">
                              {money(i.outstanding)}
                            </td>
                            <td>
                              <span className="kind-tag">{i.bucket}</span>
                            </td>
                            <td>
                              {i.kind === 'Opening' &&
                                i.outstanding === i.original &&
                                has('finance.reverse') &&
                                has('finance.manage') && (
                                  <form
                                    className="inline-form"
                                    onSubmit={(event) =>
                                      submit(
                                        event,
                                        (d) =>
                                          void write(
                                            `/openings/${i.id}/reverse`,
                                            { reason: str(d, 'reason') },
                                          ),
                                      )
                                    }
                                  >
                                    <Field
                                      label={`Reversal reason for ${i.reference}`}
                                      name="reason"
                                      required
                                      maxLength={300}
                                    />
                                    <Button disabled={busy} variant="outline">
                                      {tr('Reverse opening balance')}
                                    </Button>
                                  </form>
                                )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {has('finance.manage') && (
                    <details className="disclosure">
                      <summary>{tr('Record opening balance')}</summary>
                      <form
                        className="grid gap-3 pt-3"
                        onSubmit={(e) =>
                          submit(
                            e,
                            (d) =>
                              void write('/openings', {
                                branchId: branch,
                                customerId: supplier ? null : party,
                                supplierId: supplier ? party : null,
                                reference: str(d, 'reference'),
                                originalDate: str(d, 'originalDate'),
                                amount: Number(str(d, 'amount')),
                                reason: str(d, 'reason'),
                              }),
                          )
                        }
                      >
                        <p className="section-help">
                          {tr(
                            'Use for credit that existed before Gas365. It moves no cash; settle it later by recording a payment and allocating it.',
                          )}
                        </p>
                        <div className="form-grid">
                          <Field
                            label={tr('Original invoice date')}
                            name="originalDate"
                            type="date"
                            max={date}
                            required
                          />
                          <Field
                            label={tr('Opening amount (PKR)')}
                            name="amount"
                            type="number"
                            min="0.01"
                            step="0.01"
                            required
                          />
                          <Field
                            label={tr('Old reference')}
                            name="reference"
                            maxLength={120}
                          />
                          <Field
                            label={tr('Reconciliation reason')}
                            name="reason"
                            required
                            maxLength={300}
                          />
                        </div>
                        <div>
                          <Button disabled={busy}>
                            {tr('Record opening balance')}
                          </Button>
                        </div>
                      </form>
                    </details>
                  )}
                  <details className="disclosure">
                    <summary>{tr('Append-only credit history')}</summary>
                    <ol className="timeline pt-3">
                      {credit.data.entries.map((e) => (
                        <li key={e.id}>
                          <div className="timeline-head">
                            <strong>{label(e.kind)}</strong>
                            <span>{e.date}</span>
                          </div>
                          <p>
                            {money(e.balanceChange)} · {e.reason}
                          </p>
                        </li>
                      ))}
                    </ol>
                  </details>
                </div>
              )}
            </Card>
          )}
          {history}
        </div>
        <aside className="grid gap-6 content-start">
          {has('finance.post') && (
            <Card title={tr('Record transaction')} className="accent-card">
              <form
                className="grid gap-3"
                onSubmit={(e) =>
                  submit(e, (d) => {
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
                        ? allocationTarget(str(d, 'invoiceId'))
                        : {}),
                    });
                  })
                }
              >
                <Select
                  label={tr('Transaction type')}
                  value={kind}
                  onChange={(s) => {
                    setKind(s);
                    setParent('');
                    if (s === 'CustomerReceipt' || s === 'SupplierPayment') {
                      setSupplier(s === 'SupplierPayment');
                      setParty('');
                    }
                  }}
                  options={kinds.map((id) => ({ id, name: label(id) }))}
                />
                {kind !== 'Allocate' && (
                  <Select
                    label={tr('Payment account')}
                    name="accountId"
                    options={usableAccounts}
                  />
                )}
                {kind === 'Expense' && (
                  <Select
                    label={tr('Expense category')}
                    name="categoryId"
                    options={usableCategories}
                  />
                )}
                {kind === 'Transfer' && (
                  <Select
                    label={tr('Destination account')}
                    name="destination"
                    options={usableAccounts}
                  />
                )}
                {(kind === 'CustomerReceipt' || kind === 'SupplierPayment') && (
                  <p className="section-help">
                    {kind === 'CustomerReceipt'
                      ? tr(
                          'Payment will be recorded for the customer selected in the Udhaar panel',
                        )
                      : tr(
                          'Payment will be recorded for the supplier selected in the Udhaar panel',
                        )}
                    {party ? '' : tr(' (none selected yet)')}
                    {tr(
                      '. Leave it unapplied or allocate it to invoices afterwards.',
                    )}
                  </p>
                )}
                {(kind === 'LoanReceived' || kind === 'LoanGiven') && (
                  <Field
                    label={tr('Loan counterparty')}
                    name="counterparty"
                    required
                    maxLength={120}
                  />
                )}
                {(kind === 'Allocate' || kind === 'LoanRepayment') && (
                  <Select
                    label={
                      kind === 'Allocate'
                        ? tr('Unapplied payment')
                        : tr('Outstanding loan')
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
                          ? ['CustomerReceipt', 'SupplierPayment'].includes(
                              x.kind,
                            )
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
                    label={tr('Invoice to allocate')}
                    name="invoiceId"
                    options={(credit.data?.invoices || [])
                      .filter((x) => x.outstanding > 0)
                      .map((x) => ({
                        id: x.id,
                        name: `${x.reference} · ${money(x.outstanding)}`,
                      }))}
                  />
                )}
                <Field
                  label={tr('Amount (PKR)')}
                  name="amount"
                  type="number"
                  min="0.01"
                  step="0.01"
                  required
                />
                <Field
                  label={tr('Transaction reason / reference')}
                  name="reason"
                  required
                  maxLength={300}
                />
                <Button
                  className="w-full"
                  disabled={
                    busy ||
                    ((kind === 'CustomerReceipt' ||
                      kind === 'SupplierPayment') &&
                      !party)
                  }
                >
                  {tr('Post transaction')}
                </Button>
              </form>
            </Card>
          )}
          {section === 'expenses' && has('finance.manage') && (
            <Card title={tr('Expense categories')} icon={Tags}>
              <ul className="master-list">
                {categories.data?.map((c) => (
                  <li key={c.id}>
                    <div className="master-row">
                      <span>
                        <strong>{c.name}</strong>
                        <span className="cell-note">
                          {c.capital
                            ? tr('Capital item')
                            : tr('Operating expense')}
                        </span>
                      </span>
                      {!c.active && (
                        <StatusChip tone="neutral">{tr('Retired')}</StatusChip>
                      )}
                    </div>
                    <MasterEditor
                      label={c.name}
                      name={c.name}
                      active={c.active}
                      busy={busy}
                      onSave={(name, active) =>
                        void update(`/categories/${c.id}`, {
                          name,
                          active,
                          version: c.version,
                        })
                      }
                    />
                  </li>
                ))}
              </ul>
              <form
                className="grid gap-3 pt-4"
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
                <Field
                  label={tr('Category name')}
                  name="name"
                  required
                  maxLength={120}
                />
                <Field
                  label={tr('Capital item (excluded from operating expenses)')}
                  name="capital"
                  type="checkbox"
                />
                <Button disabled={busy} variant="outline">
                  {tr('Add category')}
                </Button>
              </form>
            </Card>
          )}
          <Card title={tr('Accounts')} icon={Landmark}>
            {accounts.data?.length === 0 && (
              <p className="section-help">
                {tr(
                  'Create an account with a reconciled opening balance. The first cash account becomes the drawer used by sales, purchases and cylinder payments. Earlier transactions remain in their original history.',
                )}
              </p>
            )}
            <ul className="master-list">
              {accounts.data?.map((a) => (
                <li key={a.id}>
                  <div className="master-row">
                    <span>
                      <strong>{a.name}</strong>
                      <span className="cell-note">
                        {a.kind}
                        {a.defaultCash ? tr(' · default drawer') : ''}
                      </span>
                    </span>
                    <span className="master-amount">
                      {money(a.balance)}
                      {!a.active && (
                        <StatusChip tone="neutral">{tr('Retired')}</StatusChip>
                      )}
                    </span>
                  </div>
                  {has('finance.manage') && (
                    <MasterEditor
                      label={a.name}
                      name={a.name}
                      active={a.active}
                      busy={busy}
                      onSave={(name, active) =>
                        void update(`/accounts/${a.id}`, {
                          name,
                          active,
                          version: a.version,
                        })
                      }
                    />
                  )}
                </li>
              ))}
            </ul>
            {has('finance.manage') && (
              <details className="disclosure">
                <summary>{tr('Create account')}</summary>
                <form
                  className="grid gap-3 pt-3"
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
                    label={tr('Account name')}
                    name="name"
                    required
                    maxLength={120}
                  />
                  <Select
                    label={tr('Account type')}
                    name="kind"
                    options={['Cash', 'Bank'].map((id) => ({ id, name: id }))}
                  />
                  <Field
                    label={tr('Reconciled opening balance (PKR)')}
                    name="opening"
                    type="number"
                    min="0"
                    step="0.01"
                    required
                  />
                  <Field
                    label={tr('Opening reconciliation reason')}
                    name="reason"
                    required
                    maxLength={300}
                  />
                  <Button disabled={busy}>{tr('Create account')}</Button>
                </form>
              </details>
            )}
          </Card>
        </aside>
      </div>
    </div>
  );
}
