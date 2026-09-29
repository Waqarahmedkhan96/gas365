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
      {message && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
      <section className="panel grid gap-4">
        <h2>1. Choose what to import</h2>
        {types.error && <p role="alert">{types.error.message}</p>}
        <form className="grid gap-3" onSubmit={upload}>
          <label className="grid gap-1">
            Data to import
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
                aria-label="Expected columns"
                tabIndex={0}
              >
                <table>
                  <caption>Expected columns</caption>
                  <thead>
                    <tr>
                      <th scope="col">Column</th>
                      <th scope="col">Required</th>
                      <th scope="col">Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {definition.fields.map((f) => (
                      <tr key={f.key}>
                        <td>{f.label}</td>
                        <td>{f.required ? 'Required' : 'Optional'}</td>
                        <td>{f.hint}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
          <label className="grid gap-1">
            Excel (.xlsx) or CSV file, up to 2 MB and 5,000 rows
            <input name="file" type="file" accept=".xlsx,.csv" required />
          </label>
          <Button disabled={busy || !definition}>Upload and check</Button>
        </form>
      </section>
      {batch && definition && (
        <section className="panel grid gap-4" aria-live="polite">
          <h2>2. Check the columns and preview</h2>
          <p>
            {batch.fileName} · {batch.rowCount} rows ·{' '}
            <strong>{importStatusText[batch.status]}</strong>
            {batch.errorCount > 0 && ` · ${batch.errorCount} rows need fixes`}
            {batch.committedCount > 0 && ` · ${batch.committedCount} imported`}
          </p>
          <form className="grid gap-3" onSubmit={remap}>
            <div className="flex flex-wrap gap-4">
              {definition.fields.map((f) => (
                <label key={f.key} className="grid gap-1">
                  {f.label}
                  {f.required ? ' (required)' : ''}
                  <select
                    name={f.key}
                    defaultValue={batch.mapping[f.key] ?? ''}
                    disabled={[
                      'Committing',
                      'Committed',
                      'PartiallyCommitted',
                    ].includes(batch.status)}
                  >
                    <option value="">Not in file</option>
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
                Match columns and check again
              </Button>
            )}
          </form>
          <div
            className="data-table report-table overflow-x-auto"
            role="region"
            aria-label="Import preview"
            tabIndex={0}
          >
            <table>
              <caption>Preview — rows with problems first</caption>
              <thead>
                <tr>
                  <th scope="col">Row</th>
                  <th scope="col">Result</th>
                  {definition.fields.map((f) => (
                    <th key={f.key} scope="col">
                      {f.label}
                    </th>
                  ))}
                  <th scope="col">Problems</th>
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
                Download error list
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
                  ? 'Retry remaining rows'
                  : `3. Import ${batch.rowCount} rows`}
              </Button>
            )}
          </div>
          <p className="section-help">
            Each row is saved through the same checks as manual entry. Retrying
            only processes rows that have not been imported, so nothing is
            duplicated.
          </p>
        </section>
      )}
      <section className="panel grid gap-3">
        <h2>Your recent imports</h2>
        {history.data?.items.length === 0 && <p>No imports yet.</p>}
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
              {importStatusText[b.status]} · {b.committedCount}/{b.rowCount}{' '}
              imported
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
