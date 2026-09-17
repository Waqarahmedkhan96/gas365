import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type PaginationState,
} from '@tanstack/react-table';
import { Button } from './ui/button';
import { PageState } from './page';

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
  if (loading)
    return (
      <PageState
        kind="loading"
        title="Loading records"
        description="Please wait while the records are loaded."
      />
    );
  if (error)
    return (
      <PageState
        kind="error"
        title="Records unavailable"
        description={error}
        retry={retry}
      />
    );
  if (!data.length)
    return (
      <PageState
        kind="empty"
        title="No records found"
        description="Try changing your filters or add a record when this module is available."
      />
    );
  return (
    <div className="data-table">
      <div className="desktop-table">
        <table>
          <caption>{title}</caption>
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => (
                  <th key={header.id} scope="col">
                    {flexRender(
                      header.column.columnDef.header,
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
      <div className="mobile-records" aria-label={title}>
        {table.getRowModel().rows.map((row) => (
          <dl key={row.id}>
            {row.getVisibleCells().map((cell) => (
              <div key={cell.id}>
                <dt>{String(cell.column.columnDef.header)}</dt>
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
          Page {pagination.pageIndex + 1} of {Math.max(1, table.getPageCount())}{' '}
          · {rowCount} records
        </p>
        <div className="flex gap-2">
          <Button
            variant="outline"
            disabled={!table.getCanPreviousPage()}
            onClick={() => table.previousPage()}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            disabled={!table.getCanNextPage()}
            onClick={() => table.nextPage()}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}
