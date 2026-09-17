import { useState } from 'react';
import { Button } from './components/ui/button';
import { Field, Input } from './components/ui/form';
import { ConfirmButton, Modal, Notification } from './components/ui/feedback';
import { PageState, StatusBadge } from './components/page';
import { DataTable } from './components/DataTable';

const samples = [
  {
    reference: 'SAMPLE-001',
    description: 'Draft example',
    status: 'Draft' as const,
  },
  {
    reference: 'SAMPLE-002',
    description: 'Posted example',
    status: 'Posted' as const,
  },
  {
    reference: 'SAMPLE-003',
    description: 'Reversal example',
    status: 'Reversed' as const,
  },
];

export function ShowcasePage() {
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 2 });
  const [search, setSearch] = useState('');
  const [notice, setNotice] = useState(0);
  const [error, setError] = useState('');
  const [state, setState] = useState<'loading' | 'empty' | 'error' | 'denied'>(
    'empty',
  );
  const filtered = samples.filter((row) =>
    `${row.reference} ${row.description}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  return (
    <div className="showcase-grid">
      <section className="panel">
        <h2>Color & typography</h2>
        <p className="section-help">
          Inter, an open font hosted with the application. Text uses accessible
          shades of the brand palette.
        </p>
        <div className="swatches">
          {['navy', 'blue', 'surface', 'success', 'warning', 'danger'].map(
            (color) => (
              <div key={color}>
                <span className={`swatch swatch-${color}`} />
                <strong>{color}</strong>
              </div>
            ),
          )}
        </div>
        <div className="flex flex-wrap gap-3">
          <StatusBadge status="Draft" />
          <StatusBadge status="Posted" />
          <StatusBadge status="Reversed" />
        </div>
      </section>
      <section className="panel">
        <h2>Actions & confirmation</h2>
        <p className="section-help">
          These examples do not create or change business records.
        </p>
        <div className="flex flex-wrap gap-3">
          <Button onClick={() => setNotice((n) => n + 1)}>
            Show notification
          </Button>
          <Button variant="secondary" disabled>
            Unavailable action
          </Button>
          <ConfirmButton
            title="Confirm sample action?"
            description="This is a component demonstration. No business data will change."
            onConfirm={() => setNotice((n) => n + 1)}
          >
            Preview confirmation
          </ConfirmButton>
          <Modal
            title="Sample record"
            description="A detail drawer for quick inspection."
            drawer
            trigger={<Button variant="outline">Preview drawer</Button>}
          >
            <dl className="detail-list">
              <div>
                <dt>Reference</dt>
                <dd>SAMPLE-001</dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>
                  <StatusBadge status="Draft" />
                </dd>
              </div>
            </dl>
          </Modal>
        </div>
      </section>
      <section className="panel">
        <h2>Form fields</h2>
        <p className="section-help">
          Required labels, help text and errors stay connected to their fields.
        </p>
        <form
          noValidate
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            const value = new FormData(event.currentTarget).get('sampleName');
            setError(String(value || '').trim() ? '' : 'Enter a sample name.');
            if (String(value || '').trim()) setNotice((n) => n + 1);
          }}
        >
          <Field
            name="sampleName"
            label="Sample name"
            hint="Use any name to try this field."
            error={error}
            required
          />
          <Field
            label="Read-only example"
            value="PKR · Pakistani rupee"
            readOnly
          />
          <Button type="submit">Validate sample</Button>
        </form>
      </section>
      <section className="panel">
        <h2>Page states</h2>
        <label className="mt-4">
          Preview state
          <select
            value={state}
            onChange={(e) => setState(e.target.value as typeof state)}
          >
            <option value="empty">Empty</option>
            <option value="loading">Loading</option>
            <option value="error">Error</option>
            <option value="denied">Permission denied</option>
          </select>
        </label>
        <PageState
          kind={state}
          title={
            {
              empty: 'Nothing here yet',
              loading: 'Loading your records',
              error: 'Unable to load records',
              denied: 'Access restricted',
            }[state]
          }
          description={
            {
              empty: 'New records will appear here.',
              loading: 'Please wait while your page loads.',
              error: 'Check your connection and try again.',
              denied: 'Ask your workspace owner if you need access.',
            }[state]
          }
          retry={state === 'error' ? () => setState('empty') : undefined}
        />
      </section>
      <section className="panel showcase-wide">
        <h2>Table & mobile records</h2>
        <p className="section-help">
          Synthetic samples only. Pagination and filters are controlled by the
          page; operational pages will fetch records from the server.
        </p>
        <label htmlFor="sample-search" className="sr-only">
          Filter sample records
        </label>
        <Input
          id="sample-search"
          placeholder="Filter sample records…"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPagination((p) => ({ ...p, pageIndex: 0 }));
          }}
        />
        <DataTable
          title="Sample records"
          data={filtered.slice(
            pagination.pageIndex * pagination.pageSize,
            (pagination.pageIndex + 1) * pagination.pageSize,
          )}
          rowCount={filtered.length}
          pagination={pagination}
          onPaginationChange={setPagination}
          columns={[
            { accessorKey: 'reference', header: 'Reference' },
            { accessorKey: 'description', header: 'Description' },
            {
              accessorKey: 'status',
              header: 'Status',
              cell: ({ row }) => <StatusBadge status={row.original.status} />,
            },
          ]}
        />
      </section>
      {!!notice && (
        <Notification
          key={notice}
          message="The sample action completed. No business data changed."
        />
      )}
    </div>
  );
}
