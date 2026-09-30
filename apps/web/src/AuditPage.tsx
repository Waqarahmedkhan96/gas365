import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import type { AuditEntry, Correction } from './operations-types';
import { businessDate, money } from './report-types';
import { tr } from './i18n';

const areas = [
  'access',
  'inventory',
  'purchasing',
  'sales',
  'finance',
  'reports',
];
const modules = [
  'stock',
  'sales',
  'purchasing',
  'finance',
  'cylinders',
  'openings',
];
type Page<T> = { items: T[]; total: number; pageSize: number };

function Pager({
  page,
  data,
  onPage,
}: {
  page: number;
  data?: Page<unknown>;
  onPage: (page: number) => void;
}) {
  if (!data || data.total <= data.pageSize) return null;
  return (
    <div className="flex gap-3">
      <Button
        variant="secondary"
        disabled={page === 1}
        onClick={() => onPage(page - 1)}
      >
        {tr('Previous')}
      </Button>
      <Button
        variant="secondary"
        disabled={page * data.pageSize >= data.total}
        onClick={() => onPage(page + 1)}
      >
        {tr('Next')}
      </Button>
    </div>
  );
}

export function AuditPage({
  user,
  branches,
  timeZone,
}: {
  user: Me;
  branches: { id: string; name: string }[];
  timeZone: string;
}) {
  const [view, setView] = useState<'timeline' | 'corrections'>('timeline');
  const [from, setFrom] = useState(businessDate(timeZone, -6));
  const [to, setTo] = useState(businessDate(timeZone));
  const [area, setArea] = useState('');
  const [module, setModule] = useState('');
  const [branch, setBranch] = useState('');
  const [page, setPage] = useState(1);
  const get = <T,>(path: string) =>
    api<T>('/api/workspace/audit' + path, 'GET', undefined, user.tenantId);
  const range = `from=${from}&to=${to}&page=${page}`;
  const timeline = useQuery({
    queryKey: ['audit', user.tenantId, range, area],
    queryFn: () =>
      get<Page<AuditEntry>>(`/timeline?${range}${area ? `&area=${area}` : ''}`),
    enabled: view === 'timeline',
  });
  const corrections = useQuery({
    queryKey: ['corrections', user.tenantId, range, module, branch],
    queryFn: () =>
      get<Page<Correction>>(
        `/corrections?${range}${module ? `&module=${module}` : ''}${branch ? `&branchId=${branch}` : ''}`,
      ),
    enabled: view === 'corrections',
  });
  const reset =
    <T,>(set: (v: T) => void) =>
    (v: T) => {
      set(v);
      setPage(1);
    };
  const when = (value: string) =>
    new Date(value).toLocaleString('en-PK', { timeZone });
  const query = view === 'timeline' ? timeline : corrections;
  return (
    <div className="grid gap-6">
      <div
        className="flex flex-wrap gap-3"
        role="group"
        aria-label={tr('View')}
      >
        <Button
          variant={view === 'timeline' ? 'default' : 'secondary'}
          aria-pressed={view === 'timeline'}
          onClick={() => reset(setView)('timeline')}
        >
          {tr('Activity timeline')}
        </Button>
        <Button
          variant={view === 'corrections' ? 'default' : 'secondary'}
          aria-pressed={view === 'corrections'}
          onClick={() => reset(setView)('corrections')}
        >
          {tr('Corrections and reversals')}
        </Button>
      </div>
      <section className="panel grid gap-4">
        <div
          className="flex flex-wrap items-end gap-4"
          role="group"
          aria-label={tr('Filters')}
        >
          <label className="grid gap-1">
            {tr('From')}
            <input
              type="date"
              value={from}
              max={to}
              onChange={(e) => reset(setFrom)(e.target.value)}
            />
          </label>
          <label className="grid gap-1">
            {tr('To')}
            <input
              type="date"
              value={to}
              onChange={(e) => reset(setTo)(e.target.value)}
            />
          </label>
          {view === 'timeline' ? (
            <label className="grid gap-1">
              {tr('Area')}
              <select
                value={area}
                onChange={(e) => reset(setArea)(e.target.value)}
              >
                <option value="">{tr('All areas')}</option>
                {areas.map((a) => (
                  <option key={a} value={a}>
                    {a[0].toUpperCase() + a.slice(1)}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <>
              <label className="grid gap-1">
                {tr('Module')}
                <select
                  value={module}
                  onChange={(e) => reset(setModule)(e.target.value)}
                >
                  <option value="">{tr('All modules')}</option>
                  {modules.map((m) => (
                    <option key={m} value={m}>
                      {m[0].toUpperCase() + m.slice(1)}
                    </option>
                  ))}
                </select>
              </label>
              <label className="grid gap-1">
                {tr('Branch')}
                <select
                  value={branch}
                  onChange={(e) => reset(setBranch)(e.target.value)}
                >
                  <option value="">{tr('All accessible branches')}</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
                </select>
              </label>
            </>
          )}
        </div>
        {query.error && <p role="alert">{query.error.message}</p>}
        {query.isPending && <p role="status">{tr('Loading…')}</p>}
        {view === 'timeline' && timeline.data && (
          <>
            <p className="section-help">
              {timeline.data.total} {tr('events in this period.')}
            </p>
            <ul className="grid gap-2">
              {timeline.data.items.map((e) => (
                <li key={e.id} className="record-row">
                  <div className="grid gap-1">
                    <strong>
                      {e.action.replaceAll('.', ' ').replaceAll('_', ' ')}
                    </strong>
                    <span className="section-help">
                      {e.actor} · {e.area}
                      {e.subjectId
                        ? ` · record ${e.subjectId.slice(0, 8)}`
                        : ''}
                    </span>
                  </div>
                  <time dateTime={e.occurredAt}>{when(e.occurredAt)}</time>
                </li>
              ))}
            </ul>
            <Pager page={page} data={timeline.data} onPage={setPage} />
          </>
        )}
        {view === 'corrections' && corrections.data && (
          <>
            <p className="section-help">
              {tr(
                'Posted records are never edited. Each correction below is a separate reversal with its reason and the person who made it.',
              )}
            </p>
            {corrections.data.items.length === 0 ? (
              <p>{tr('No corrections in this period.')}</p>
            ) : (
              <div
                className="data-table report-table overflow-x-auto"
                role="region"
                aria-label={tr('Corrections table')}
                tabIndex={0}
              >
                <table>
                  <caption>
                    {tr('Corrections —')} {corrections.data.total}{' '}
                    {tr('in period')}
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">{tr('Date')}</th>
                      <th scope="col">{tr('Module')}</th>
                      <th scope="col">{tr('Correction')}</th>
                      <th scope="col">{tr('Branch')}</th>
                      <th scope="col" className="text-right">
                        {tr('Amount')}
                      </th>
                      <th scope="col">{tr('Reason')}</th>
                      <th scope="col">{tr('By')}</th>
                      <th scope="col">{tr('Original record')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {corrections.data.items.map((c) => (
                      <tr key={c.reversalId}>
                        <td>{c.businessDate}</td>
                        <td>{c.module}</td>
                        <td>{c.kind}</td>
                        <td>{c.branch}</td>
                        <td className="text-right tabular-nums">
                          {money(c.amount)}
                        </td>
                        <td>{c.reason}</td>
                        <td>{c.actor}</td>
                        <td>{c.originalId.slice(0, 8)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <Pager page={page} data={corrections.data} onPage={setPage} />
          </>
        )}
      </section>
    </div>
  );
}
