import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import {
  businessDate,
  formatCell,
  isNumeric,
  type ExportJob,
  type Report,
  type ReportDefinition,
} from './report-types';

const root = '/api/workspace/reports';
const statusText: Record<ExportJob['status'], string> = {
  Queued: 'Queued',
  Running: 'Preparing',
  Completed: 'Ready to download',
  Failed: 'Failed',
  Expired: 'Expired',
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
  if (definitions.error) return <p role="alert">{definitions.error.message}</p>;
  if (!definitions.data) return <p role="status">Loading reports…</p>;
  if (!definition) return <p>Your role does not include any reports yet.</p>;
  const data = report.data;
  return (
    <div className="grid gap-6">
      {message && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
      <section className="panel grid gap-4">
        <div
          className="flex flex-wrap items-end gap-4"
          role="group"
          aria-label="Report filters"
        >
          <label className="grid gap-1">
            Report
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
              Branch
              <select
                value={branch}
                onChange={(e) => {
                  setBranch(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All accessible branches</option>
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
              From
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
            {definition.dateRange ? 'To' : 'As of'}
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
              Type
              <select
                value={type}
                onChange={(e) => {
                  setType(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All</option>
                {definition.types.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
        <div>
          <h2>{definition.title}</h2>
          <p>{definition.definition}</p>
          <p className="section-help">
            Reconciliation: {definition.reconciliation}
          </p>
        </div>
        {canExport && (
          <div className="flex flex-wrap gap-3" aria-label="Export">
            {(['csv', 'xlsx', 'pdf'] as const).map((f) => (
              <Button
                key={f}
                variant="secondary"
                onClick={() => void requestExport(f)}
              >
                Export {f.toUpperCase()}
              </Button>
            ))}
          </div>
        )}
      </section>
      <section className="panel grid gap-4" aria-busy={report.isFetching}>
        {report.error && <p role="alert">{report.error.message}</p>}
        {report.isPending && <p role="status">Loading report…</p>}
        {data && (
          <>
            <p className="section-help">
              {data.scope} · {data.totalRows} rows · generated{' '}
              {new Date(data.generatedAt).toLocaleString('en-PK', {
                timeZone,
              })}
            </p>
            {data.totalRows === 0 ? (
              <p>No activity for these filters.</p>
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
                    {data.title} — page {data.page}
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
                              ? 'Total'
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
                  variant="secondary"
                  disabled={page === 1}
                  onClick={() => setPage(page - 1)}
                >
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  disabled={page * data.pageSize >= data.totalRows}
                  onClick={() => setPage(page + 1)}
                >
                  Next
                </Button>
              </div>
            )}
          </>
        )}
      </section>
      {canExport && (
        <section className="panel grid gap-3">
          <h2>Your exports</h2>
          <p className="section-help">
            Files are private to you and expire 24 hours after they are ready.
          </p>
          {exports.data?.items.length === 0 && <p>No exports yet.</p>}
          <ul className="grid gap-2">
            {exports.data?.items.map((job) => (
              <li key={job.id}>
                {titles.get(job.reportKey) ?? job.reportKey} ·{' '}
                {job.format.toUpperCase()} · {statusText[job.status]}
                {job.status === 'Completed' && (
                  <>
                    {' '}
                    ·{' '}
                    <Button
                      variant="secondary"
                      onClick={() => void download(job)}
                    >
                      Download {job.fileName}
                    </Button>
                  </>
                )}
                {job.error && ` · ${job.error}`}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
