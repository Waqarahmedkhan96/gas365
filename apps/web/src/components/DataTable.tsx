import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type PaginationState,
} from '@tanstack/react-table';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './ui/button';
import { PageState } from './page';
import { tr } from '../i18n';

export function DataTable<T>({
  title,
  columns,
  data,
  rowCount,
  pagination,
  onPaginationChange,
  loading = false,
  error,
  retry,
}: {
  title: string;
  columns: (ColumnDef<T> & { header: string })[];
  data: T[];
  rowCount: number;
  pagination: PaginationState;
  onPaginationChange: OnChangeFn<PaginationState>;
  loading?: boolean;
  error?: string;
  retry?: () => void;
}) {
  // Pagination is controlled by the caller. Production callers fetch one server page.
  // eslint-disable-next-line react-hooks/incompatible-library -- React Compiler is not enabled; the v8 table API is intentionally mutable.
  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    manualPagination: true,
    rowCount,
    state: { pagination },
    onPaginationChange,
  });
  const first = rowCount ? pagination.pageIndex * pagination.pageSize + 1 : 0;
  const last = Math.min(
    rowCount,
    (pagination.pageIndex + 1) * pagination.pageSize,
  );
  if (loading)
    return (
      <PageState
        kind="loading"
        title={tr('Loading records')}
        description={tr('Please wait while the records are loaded.')}
      />
    );
  if (error)
    return (
      <PageState
        kind="error"
        title={tr('Records unavailable')}
        description={error}
        retry={retry}
      />
    );
  if (!data.length)
    return (
      <PageState
        kind="empty"
        title={tr('No records found')}
        description={tr('Try changing your filters, or add a new record.')}
      />
    );
  return (
    <div className="data-table">
      <div className="desktop-table">
        <table>
          <caption>{tr(title)}</caption>
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => (
                  <th key={header.id} scope="col">
                    {flexRender(
                      typeof header.column.columnDef.header === 'string'
                        ? tr(header.column.columnDef.header)
                        : header.column.columnDef.header,
                      header.getContext(),
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => (
              <tr key={row.id}>
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id}>
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="mobile-records" aria-label={tr(title)}>
        {table.getRowModel().rows.map((row) => (
          <dl key={row.id}>
            {row.getVisibleCells().map((cell) => (
              <div key={cell.id}>
                <dt>{tr(String(cell.column.columnDef.header))}</dt>
                <dd>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </dd>
              </div>
            ))}
          </dl>
        ))}
      </div>
      <div className="pagination">
        <p aria-live="polite">
          {tr(
            'Showing {first}–{last} of {total} records · Page {page} of {pages}',
            {
              first: first,
              last: last,
              total: rowCount,
              page: pagination.pageIndex + 1,
              pages: Math.max(1, table.getPageCount()),
            },
          )}
        </p>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={!table.getCanPreviousPage()}
            onClick={() => table.previousPage()}
          >
            <ChevronLeft aria-hidden="true" size={16} /> {tr('Previous')}
          </Button>
          <Button
            variant="outline"
            disabled={!table.getCanNextPage()}
            onClick={() => table.nextPage()}
          >
            {tr('Next')} <ChevronRight aria-hidden="true" size={16} />
          </Button>
        </div>
      </div>
    </div>
  );
}
