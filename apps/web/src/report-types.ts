export type ReportColumn = {
  key: string;
  label: string;
  type: 'text' | 'date' | 'money' | 'quantity' | 'count' | 'percent' | 'cost';
};
export type Cell = string | number | null;
export type ReportDefinition = {
  key: string;
  title: string;
  definition: string;
  reconciliation: string;
  types: string[];
  dateRange: boolean;
  branchScoped: boolean;
};
export type Report = {
  key: string;
  title: string;
  definition: string;
  scope: string;
  columns: ReportColumn[];
  rows: Cell[][];
  totals: Cell[];
  totalRows: number;
  page: number;
  pageSize: number;
  generatedAt: string;
};
export type ExportJob = {
  id: string;
  reportKey: string;
  format: 'csv' | 'xlsx' | 'pdf';
  status: 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Expired';
  attempts: number;
  error: string;
  fileName: string;
  rowCount: number;
  requestedAt: string;
  completedAt: string | null;
  expiresAt: string | null;
};
export type Dashboard = {
  from: string;
  to: string;
  scope: string;
  metrics: {
    key: string;
    label: string;
    value: number;
    type: 'money' | 'count';
  }[];
  trend: {
    date: string;
    revenue: number;
    grossProfit: number | null;
    expenses: number | null;
  }[];
  expensesByCategory: { label: string; value: number }[];
  revenueByBranch: { label: string; value: number }[];
  profitVisible: boolean;
  financeVisible: boolean;
};

export const money = (n: number) =>
  new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    currencyDisplay: 'code',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n);

// Presentation only: every value arrives already calculated by the server.
export function formatCell(value: Cell, type: ReportColumn['type']) {
  if (value === null || value === '') return '';
  if (typeof value === 'string') return value;
  switch (type) {
    case 'money':
      return money(value);
    case 'percent':
      return `${value.toFixed(2)}%`;
    case 'count':
      return new Intl.NumberFormat('en-PK').format(value);
    case 'cost':
      return new Intl.NumberFormat('en-PK', {
        maximumFractionDigits: 6,
      }).format(value);
    default:
      return new Intl.NumberFormat('en-PK', {
        maximumFractionDigits: 3,
      }).format(value);
  }
}

export const isNumeric = (type: ReportColumn['type']) =>
  type !== 'text' && type !== 'date';

// Business dates in the tenant time zone, as yyyy-mm-dd.
export function businessDate(timeZone: string, offsetDays = 0) {
  const date = new Date(Date.now() + offsetDays * 86_400_000);
  return date.toLocaleDateString('en-CA', { timeZone });
}
