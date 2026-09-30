import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import { CheckCircle2, Download, FileBarChart, FileDown } from 'lucide-react';
import {
  Card,
  PageState,
  StatCard,
  StatGrid,
  StatusChip,
  type Tone,
} from './components/page';
import { PageActions } from './components/page-actions';
import {
  businessDate,
  formatCell,
  isNumeric,
  type ExportJob,
  type Report,
  type ReportDefinition,
} from './report-types';
import { tr } from './i18n';

const root = '/api/workspace/reports';
const statusText: Record<ExportJob['status'], string> = {
  Queued: 'Queued',
  Running: 'Preparing',
  Completed: 'Ready to download',
  Failed: 'Failed',
  Expired: 'Expired',
};

const exportTone: Record<ExportJob['status'], Tone> = {
  Queued: 'neutral',
  Running: 'info',
  Completed: 'success',
  Failed: 'danger',
  Expired: 'warning',
};

export function ReportsPage({
  user,
  branches,
  timeZone,
}: {
  user: Me;
  branches: { id: string; name: string }[];
  timeZone: string;
}) {
  const cache = useQueryClient();
  const canExport = user.permissions.includes('reports.export');
  const [key, setKey] = useState('');
  const [branch, setBranch] = useState('');
  const [from, setFrom] = useState(businessDate(timeZone, -29));
  const [to, setTo] = useState(businessDate(timeZone));
  const [type, setType] = useState('');
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const pending = useRef(new Map<string, string>());
  const get = <T,>(path: string) =>
    api<T>(root + path, 'GET', undefined, user.tenantId);
  const definitions = useQuery({
    queryKey: ['reports', user.tenantId, 'definitions'],
    queryFn: () => get<ReportDefinition[]>(''),
  });
  const definition =
    definitions.data?.find((d) => d.key === key) ?? definitions.data?.[0];
  const start = definition?.dateRange ? from : to;
  const query = `from=${start}&to=${to}${branch && definition?.branchScoped ? `&branchId=${branch}` : ''}${type ? `&type=${encodeURIComponent(type)}` : ''}`;
  const report = useQuery({
    queryKey: ['reports', user.tenantId, definition?.key, query, page],
    queryFn: () => get<Report>(`/${definition!.key}?${query}&page=${page}`),
    enabled: !!definition,
  });
  const exports = useQuery({
    queryKey: ['reports', user.tenantId, 'exports'],
    queryFn: () => get<{ items: ExportJob[] }>('/exports'),
    enabled: canExport,
    refetchInterval: (q) =>
      q.state.data?.items.some((x) => ['Queued', 'Running'].includes(x.status))
        ? 2000
        : false,
  });

  async function requestExport(format: ExportJob['format']) {
    if (!definition) return;
    setError('');
    setMessage('');
    const body = {
      reportKey: definition.key,
      format,
      branchId: definition.branchScoped && branch ? branch : null,
      from: start,
      to,
      type: type || null,
    };
    // Reuse the key while the same request is retried, so a lost response cannot queue a duplicate.
    const fingerprint = JSON.stringify(body);
    const idempotencyKey =
      pending.current.get(fingerprint) || crypto.randomUUID();
    pending.current.set(fingerprint, idempotencyKey);
    try {
      await api(
        root + '/exports',
        'POST',
        { ...body, idempotencyKey },
        user.tenantId,
      );
      pending.current.delete(fingerprint);
      setMessage(
        `${format.toUpperCase()} export queued. It will appear below when ready.`,
      );
      await cache.invalidateQueries({
        queryKey: ['reports', user.tenantId, 'exports'],
      });
    } catch (e) {
      setError((e as Error).message);
    }
  }

  // Workspace requests need the X-Workspace header, so the file is fetched and saved from a blob URL.
  async function download(job: ExportJob) {
    setError('');
    const response = await fetch(`${root}/exports/${job.id}/download`, {
      credentials: 'same-origin',
      headers: { 'X-Workspace': user.tenantId },
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}));
      setError(problem.title || 'The download could not be completed.');
      await cache.invalidateQueries({
        queryKey: ['reports', user.tenantId, 'exports'],
      });
      return;
    }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = job.fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  const titles = new Map(definitions.data?.map((d) => [d.key, d.title]));
  if (definitions.error)
    return (
      <PageState
        kind="error"
        title={tr('Reports unavailable')}
        description={definitions.error.message}
        retry={() => void definitions.refetch()}
      />
    );
  if (!definitions.data)
    return (
      <PageState
        kind="loading"
        title={tr('Loading reports')}
        description={tr('Please wait while your reports are loaded.')}
      />
    );
  if (!definition)
    return (
      <PageState
        kind="denied"
        title={tr('No reports available')}
        description={tr('Your role does not include any reports yet.')}
      />
    );
  const data = report.data;
  const presets = [
    {
      label: 'Today',
      from: businessDate(timeZone),
      to: businessDate(timeZone),
    },
    {
      label: 'Last 7 days',
      from: businessDate(timeZone, -6),
      to: businessDate(timeZone),
    },
    {
      label: 'This month',
      from: businessDate(timeZone).slice(0, 8) + '01',
      to: businessDate(timeZone),
    },
    {
      label: 'Last 30 days',
      from: businessDate(timeZone, -29),
      to: businessDate(timeZone),
    },
    {
      label: 'This year',
      from: businessDate(timeZone).slice(0, 5) + '01-01',
      to: businessDate(timeZone),
    },
  ];
  // Headline cards repeat the server's own totals row; nothing is summed in the browser.
  const headline = data
    ? data.columns
        .map((c, i) => ({ column: c, total: data.totals[i] }))
        .filter((x, i) => i > 0 && x.total !== null && isNumeric(x.column.type))
        .slice(0, 4)
    : [];
  return (
    <div className="grid gap-6">
      {canExport && (
        <PageActions>
          {(['csv', 'xlsx', 'pdf'] as const).map((f) => (
            <Button
              key={f}
              variant={f === 'pdf' ? 'default' : 'outline'}
              onClick={() => void requestExport(f)}
            >
              <Download aria-hidden="true" size={16} />
              {tr('Export')} {f.toUpperCase()}
            </Button>
          ))}
        </PageActions>
      )}
      {message && (
        <p role="status" className="notice-banner">
          <CheckCircle2 aria-hidden="true" size={18} /> {message}
        </p>
      )}
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      <section className="filter-bar report-filters">
        <div
          className="flex flex-wrap items-end gap-4"
          role="group"
          aria-label={tr('Report filters')}
        >
          <label className="grid gap-1">
            {tr('Report')}
            <select
              value={definition.key}
              onChange={(e) => {
                setKey(e.target.value);
                setType('');
                setPage(1);
              }}
            >
              {definitions.data.map((d) => (
                <option key={d.key} value={d.key}>
                  {d.title}
                </option>
              ))}
            </select>
          </label>
          {definition.branchScoped && (
            <label className="grid gap-1">
              {tr('Branch')}
              <select
                value={branch}
                onChange={(e) => {
                  setBranch(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">{tr('All accessible branches')}</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          {definition.dateRange && (
            <label className="grid gap-1">
              {tr('From')}
              <input
                type="date"
                value={from}
                max={to}
                onChange={(e) => {
                  setFrom(e.target.value);
                  setPage(1);
                }}
              />
            </label>
          )}
          <label className="grid gap-1">
            {definition.dateRange ? tr('To') : tr('As of')}
            <input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
              }}
            />
          </label>
          {definition.types.length > 0 && (
            <label className="grid gap-1">
              {tr('Type')}
              <select
                value={type}
                onChange={(e) => {
                  setType(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">{tr('All')}</option>
                {definition.types.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
        {definition.dateRange && (
          <div
            className="preset-row"
            role="group"
            aria-label={tr('Quick periods')}
          >
            {presets.map((p) => (
              <button
                key={p.label}
                type="button"
                className="preset-pill"
                aria-pressed={from === p.from && to === p.to}
                onClick={() => {
                  setFrom(p.from);
                  setTo(p.to);
                  setPage(1);
                }}
              >
                {p.label}
              </button>
            ))}
          </div>
        )}
      </section>
      <div className="report-intro">
        <span className="report-intro-icon" aria-hidden="true">
          <FileBarChart size={22} />
        </span>
        <div>
          <h2>{definition.title}</h2>
          <p>{definition.definition}</p>
          <p className="section-help">
            {tr('Reconciliation:')} {definition.reconciliation}
          </p>
        </div>
      </div>
      {headline.length > 0 && (
        <StatGrid label={tr('Report totals')}>
          {headline.map(({ column, total }) => (
            <StatCard
              key={column.key}
              label={column.label}
              value={formatCell(total, column.type)}
              note={tr('Total for these filters')}
            />
          ))}
        </StatGrid>
      )}
      <section className="card grid gap-4" aria-busy={report.isFetching}>
        {report.error && <p role="alert">{report.error.message}</p>}
        {report.isPending && <p role="status">{tr('Loading report…')}</p>}
        {data && (
          <>
            <p className="section-help">
              {tr('{scope} · {rows} rows · generated {time}', {
                scope: data.scope,
                rows: data.totalRows,
                time: new Date(data.generatedAt).toLocaleString('en-PK', {
                  timeZone,
                }),
              })}
            </p>
            {data.totalRows === 0 ? (
              <PageState
                kind="empty"
                title={tr('No activity')}
                description={tr('No activity for these filters.')}
              />
            ) : (
              // Focusable so keyboard users can scroll wide tables on small screens.
              <div
                className="data-table report-table overflow-x-auto"
                role="region"
                aria-label={`${data.title} table`}
                tabIndex={0}
              >
                <table>
                  <caption>
                    {tr('{title} — page {page}', {
                      title: data.title,
                      page: data.page,
                    })}
                  </caption>
                  <thead>
                    <tr>
                      {data.columns.map((c) => (
                        <th
                          key={c.key}
                          scope="col"
                          className={isNumeric(c.type) ? 'text-right' : ''}
                        >
                          {c.label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {data.rows.map((row, r) => (
                      <tr key={r}>
                        {row.map((cell, i) => (
                          <td
                            key={data.columns[i].key}
                            className={
                              isNumeric(data.columns[i].type)
                                ? 'text-right tabular-nums'
                                : ''
                            }
                          >
                            {formatCell(cell, data.columns[i].type)}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                  {data.totals.some((t) => t !== null) && (
                    <tfoot>
                      <tr>
                        {data.totals.map((t, i) => (
                          <th
                            key={data.columns[i].key}
                            scope={i === 0 ? 'row' : undefined}
                            className={
                              isNumeric(data.columns[i].type)
                                ? 'text-right tabular-nums'
                                : ''
                            }
                          >
                            {i === 0 && t === null
                              ? tr('Total')
                              : formatCell(t, data.columns[i].type)}
                          </th>
                        ))}
                      </tr>
                    </tfoot>
                  )}
                </table>
              </div>
            )}
            {data.totalRows > data.pageSize && (
              <div className="flex gap-3">
                <Button
                  variant="outline"
                  disabled={page === 1}
                  onClick={() => setPage(page - 1)}
                >
                  {tr('Previous')}
                </Button>
                <Button
                  variant="outline"
                  disabled={page * data.pageSize >= data.totalRows}
                  onClick={() => setPage(page + 1)}
                >
                  {tr('Next')}
                </Button>
              </div>
            )}
          </>
        )}
      </section>
      {canExport && (
        <Card title={tr('Your exports')} icon={FileDown}>
          <p className="section-help">
            {tr(
              'Files are private to you and expire 24 hours after they are ready.',
            )}
          </p>
          {exports.data?.items.length === 0 && (
            <p className="muted-text pt-3">{tr('No exports yet.')}</p>
          )}
          <ul className="export-list">
            {exports.data?.items.map((job) => (
              <li key={job.id}>
                <div>
                  <strong>{titles.get(job.reportKey) ?? job.reportKey}</strong>
                  <span className="cell-note">
                    {job.format.toUpperCase()}
                    {job.error ? ` · ${job.error}` : ''}
                  </span>
                </div>
                <div className="export-actions">
                  <StatusChip tone={exportTone[job.status]}>
                    {tr(statusText[job.status])}
                  </StatusChip>
                  {job.status === 'Completed' && (
                    <Button
                      variant="outline"
                      onClick={() => void download(job)}
                    >
                      <Download aria-hidden="true" size={16} />
                      {tr('Download')} {job.fileName}
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  );
}
