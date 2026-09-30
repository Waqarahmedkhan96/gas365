import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import {
  downloadFile,
  importStatusText,
  type ImportBatch,
  type ImportType,
} from './operations-types';
import { tr } from './i18n';

const root = '/api/workspace/imports';

export function ImportsPage({ user }: { user: Me }) {
  const cache = useQueryClient();
  const [type, setType] = useState('');
  const [batch, setBatch] = useState<ImportBatch | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const types = useQuery({
    queryKey: ['imports', user.tenantId, 'types'],
    queryFn: () =>
      api<ImportType[]>(root + '/types', 'GET', undefined, user.tenantId),
  });
  const history = useQuery({
    queryKey: ['imports', user.tenantId, 'history'],
    queryFn: () =>
      api<{ items: ImportBatch[] }>(root, 'GET', undefined, user.tenantId),
  });
  const definition =
    types.data?.find((t) => t.key === (batch?.type ?? type)) ?? types.data?.[0];

  async function run(action: () => Promise<ImportBatch | void>, done = '') {
    if (busy) return;
    setBusy(true);
    setError('');
    setMessage('');
    try {
      const result = await action();
      if (result) setBatch(result);
      if (done) setMessage(done);
      await cache.invalidateQueries({
        queryKey: ['imports', user.tenantId, 'history'],
      });
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  // The file travels as the raw request body; the server checks its content, size and row count.
  function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const file = new FormData(event.currentTarget).get('file') as File | null;
    if (!file || !file.name || !definition) return;
    void run(async () => {
      const csrf = await (
        await fetch('/api/auth/csrf', { credentials: 'same-origin' })
      ).json();
      const response = await fetch(
        `${root}?type=${definition.key}&fileName=${encodeURIComponent(file.name)}`,
        {
          method: 'POST',
          credentials: 'same-origin',
          headers: {
            'X-Workspace': user.tenantId,
            'X-CSRF-TOKEN': csrf.token,
            'Content-Type': 'application/octet-stream',
          },
          body: file,
        },
      );
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new Error(problem.title || 'The file could not be uploaded.');
      }
      return (await response.json()) as ImportBatch;
    }, 'File checked. Review the column matching and preview below.');
  }

  function remap(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!batch || !definition) return;
    const data = new FormData(event.currentTarget);
    const mapping = Object.fromEntries(
      definition.fields.map((f) => [
        f.key,
        String(data.get(f.key) || '') || null,
      ]),
    );
    void run(
      () =>
        api<ImportBatch>(
          `${root}/${batch.id}/mapping`,
          'PUT',
          { mapping, version: batch.version },
          user.tenantId,
        ),
      'Columns matched and every row checked again.',
    );
  }

  const canCommit =
    batch && ['Validated', 'PartiallyCommitted'].includes(batch.status);
  return (
    <div className="grid gap-6">
      {message && (
        <p role="status" className="notice-banner">
          {message}
        </p>
      )}
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      <section className="panel grid gap-4">
        <h2>{tr('1. Choose what to import')}</h2>
        {types.error && <p role="alert">{types.error.message}</p>}
        <form className="grid gap-3" onSubmit={upload}>
          <label className="grid gap-1">
            {tr('Data to import')}
            <select
              value={definition?.key ?? ''}
              onChange={(e) => {
                setType(e.target.value);
                setBatch(null);
              }}
            >
              {types.data?.map((t) => (
                <option key={t.key} value={t.key}>
                  {t.title}
                </option>
              ))}
            </select>
          </label>
          {definition && (
            <>
              <p>{definition.description}</p>
              <div
                className="data-table report-table overflow-x-auto"
                role="region"
                aria-label={tr('Expected columns')}
                tabIndex={0}
              >
                <table>
                  <caption>{tr('Expected columns')}</caption>
                  <thead>
                    <tr>
                      <th scope="col">{tr('Column')}</th>
                      <th scope="col">{tr('Required')}</th>
                      <th scope="col">{tr('Notes')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {definition.fields.map((f) => (
                      <tr key={f.key}>
                        <td>{f.label}</td>
                        <td>{f.required ? tr('Required') : tr('Optional')}</td>
                        <td>{f.hint}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
          <label className="grid gap-1">
            {tr('Excel (.xlsx) or CSV file, up to 2 MB and 5,000 rows')}
            <input name="file" type="file" accept=".xlsx,.csv" required />
          </label>
          <Button disabled={busy || !definition}>
            {tr('Upload and check')}
          </Button>
        </form>
      </section>
      {batch && definition && (
        <section className="panel grid gap-4" aria-live="polite">
          <h2>{tr('2. Check the columns and preview')}</h2>
          <p>
            {tr('{file} · {rows} rows ·', {
              file: batch.fileName,
              rows: batch.rowCount,
            })}{' '}
            <strong>{tr(importStatusText[batch.status])}</strong>
            {batch.errorCount > 0 &&
              tr(' · {count} rows need fixes', { count: batch.errorCount })}
            {batch.committedCount > 0 &&
              tr(' · {count} imported', { count: batch.committedCount })}
          </p>
          <form className="grid gap-3" onSubmit={remap}>
            <div className="flex flex-wrap gap-4">
              {definition.fields.map((f) => (
                <label key={f.key} className="grid gap-1">
                  {f.label}
                  {f.required ? tr(' (required)') : ''}
                  <select
                    name={f.key}
                    defaultValue={batch.mapping[f.key] ?? ''}
                    disabled={[
                      'Committing',
                      'Committed',
                      'PartiallyCommitted',
                    ].includes(batch.status)}
                  >
                    <option value="">{tr('Not in file')}</option>
                    {batch.headers.map((h) => (
                      <option key={h} value={h}>
                        {h}
                      </option>
                    ))}
                  </select>
                </label>
              ))}
            </div>
            {['Uploaded', 'Validated', 'Invalid'].includes(batch.status) && (
              <Button variant="secondary" disabled={busy}>
                {tr('Match columns and check again')}
              </Button>
            )}
          </form>
          <div
            className="data-table report-table overflow-x-auto"
            role="region"
            aria-label={tr('Import preview')}
            tabIndex={0}
          >
            <table>
              <caption>{tr('Preview — rows with problems first')}</caption>
              <thead>
                <tr>
                  <th scope="col">{tr('Row')}</th>
                  <th scope="col">{tr('Result')}</th>
                  {definition.fields.map((f) => (
                    <th key={f.key} scope="col">
                      {f.label}
                    </th>
                  ))}
                  <th scope="col">{tr('Problems')}</th>
                </tr>
              </thead>
              <tbody>
                {batch.preview.map((row) => (
                  <tr key={row.rowNumber}>
                    <td>{row.rowNumber}</td>
                    <td>{row.status}</td>
                    {definition.fields.map((f) => (
                      <td key={f.key}>{row.values[f.key]}</td>
                    ))}
                    <td>{row.errors.join(' ')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex flex-wrap gap-3">
            {batch.errorCount > 0 && (
              <Button
                variant="secondary"
                onClick={() =>
                  void run(() =>
                    downloadFile(
                      `${root}/${batch.id}/errors`,
                      user.tenantId,
                      batch.fileName.replace(/\.[^.]+$/, '') + '-errors.csv',
                    ),
                  )
                }
              >
                {tr('Download error list')}
              </Button>
            )}
            {canCommit && (
              <Button
                disabled={busy}
                onClick={() =>
                  void run(
                    () =>
                      api<ImportBatch>(
                        `${root}/${batch.id}/commit`,
                        'POST',
                        { version: batch.version },
                        user.tenantId,
                      ),
                    'Import finished. Review the result above.',
                  )
                }
              >
                {batch.status === 'PartiallyCommitted'
                  ? tr('Retry remaining rows')
                  : `3. Import ${batch.rowCount} rows`}
              </Button>
            )}
          </div>
          <p className="section-help">
            {tr(
              'Each row is saved through the same checks as manual entry. Retrying only processes rows that have not been imported, so nothing is duplicated.',
            )}
          </p>
        </section>
      )}
      <section className="panel grid gap-3">
        <h2>{tr('Your recent imports')}</h2>
        {history.data?.items.length === 0 && <p>{tr('No imports yet.')}</p>}
        <ul className="grid gap-2">
          {history.data?.items.map((b) => (
            <li key={b.id}>
              <button
                type="button"
                className="link-button"
                onClick={() =>
                  void run(() =>
                    api<ImportBatch>(
                      `${root}/${b.id}`,
                      'GET',
                      undefined,
                      user.tenantId,
                    ),
                  )
                }
              >
                {b.fileName}
              </button>{' '}
              · {types.data?.find((t) => t.key === b.type)?.title ?? b.type} ·{' '}
              {tr(importStatusText[b.status])} · {b.committedCount}/{b.rowCount}{' '}
              {tr('imported')}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
