import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { api, type Me } from './access-api';
import { useBranch } from './components/branch-context';
import {
  businessDate,
  money,
  type Dashboard as DashboardData,
} from './report-types';

// Validated categorical slots (light surface #FFFFFF): blue, orange, aqua. Aqua is below 3:1,
// so every chart also has a legend, a dashed secondary encoding and a table view.
const series = { revenue: '#2a78d6', profit: '#eb6834', expenses: '#1baf7a' };
const ink = { text: '#1C2B3A', axis: '#52606D', grid: '#E4EAF0' };
const compact = (n: number) =>
  new Intl.NumberFormat('en-PK', {
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(n);

function TableView({
  caption,
  headers,
  rows,
}: {
  caption: string;
  headers: string[];
  rows: (string | number)[][];
}) {
  return (
    <details>
      <summary>Show {caption.toLowerCase()} as a table</summary>
      <div
        className="data-table report-table overflow-x-auto"
        role="region"
        aria-label={`${caption} table`}
        tabIndex={0}
      >
        <table>
          <caption>{caption}</caption>
          <thead>
            <tr>
              {headers.map((h) => (
                <th key={h} scope="col">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={String(row[0])}>
                {row.map((cell, i) => (
                  <td key={i}>
                    {typeof cell === 'number' ? money(cell) : cell}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}

function Ranked({
  title,
  data,
}: {
  title: string;
  data: { label: string; value: number }[];
}) {
  return (
    <section className="panel grid gap-3" aria-label={title}>
      <h2>{title}</h2>
      <div style={{ height: Math.max(160, data.length * 36) }}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} layout="vertical" margin={{ left: 8 }}>
            <CartesianGrid horizontal={false} stroke={ink.grid} />
            <XAxis
              type="number"
              tickFormatter={compact}
              stroke={ink.axis}
              fontSize={12}
            />
            <YAxis
              type="category"
              dataKey="label"
              width={120}
              stroke={ink.axis}
              fontSize={12}
            />
            <Tooltip formatter={(v) => money(Number(v))} />
            <Bar
              dataKey="value"
              name={title}
              fill={series.revenue}
              radius={[0, 4, 4, 0]}
              barSize={18}
              isAnimationActive={false}
            />
          </BarChart>
        </ResponsiveContainer>
      </div>
      <TableView
        caption={title}
        headers={['Name', 'Amount']}
        rows={data.map((x) => [x.label, x.value])}
      />
    </section>
  );
}

export function Dashboard({ user, timeZone }: { user: Me; timeZone: string }) {
  const branch = useBranch();
  const [days, setDays] = useState(30);
  const [scoped, setScoped] = useState(false);
  const from = businessDate(timeZone, -(days - 1));
  const to = businessDate(timeZone);
  const dashboard = useQuery({
    queryKey: ['dashboard', user.tenantId, from, to, scoped && branch],
    queryFn: () =>
      api<DashboardData>(
        `/api/workspace/reports/dashboard?from=${from}&to=${to}${scoped && branch ? `&branchId=${branch}` : ''}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  const data = dashboard.data;
  const lines = [
    { key: 'revenue', label: 'Net revenue', color: series.revenue },
    ...(data?.profitVisible
      ? [{ key: 'grossProfit', label: 'Gross profit', color: series.profit }]
      : []),
    ...(data?.financeVisible
      ? [
          {
            key: 'expenses',
            label: 'Operating expenses',
            color: series.expenses,
            dashed: true,
          },
        ]
      : []),
  ];
  return (
    <div className="grid gap-6">
      <div
        className="flex flex-wrap items-end gap-4"
        role="group"
        aria-label="Dashboard filters"
      >
        <label className="grid gap-1">
          Period
          <select
            value={days}
            onChange={(e) => setDays(Number(e.target.value))}
          >
            <option value={7}>Last 7 days</option>
            <option value={30}>Last 30 days</option>
            <option value={90}>Last 90 days</option>
          </select>
        </label>
        <label className="grid gap-1">
          Branches
          <select
            value={scoped ? 'current' : 'all'}
            onChange={(e) => setScoped(e.target.value === 'current')}
          >
            <option value="all">All accessible branches</option>
            <option value="current" disabled={!branch}>
              Selected branch
            </option>
          </select>
        </label>
      </div>
      {dashboard.isPending && <p role="status">Loading dashboard…</p>}
      {dashboard.error && <p role="alert">{dashboard.error.message}</p>}
      {data && (
        <>
          <p className="section-help">
            {data.scope} · {data.from} to {data.to}. Figures are calculated by
            the server from posted ledgers.
          </p>
          <div className="summary-grid" aria-label="Key figures">
            {data.metrics.map((m) => (
              <div key={m.key}>
                <strong>{m.type === 'money' ? money(m.value) : m.value}</strong>
                <span>{m.label}</span>
              </div>
            ))}
          </div>
          {data.trend.length > 0 && (
            <section className="panel grid gap-3" aria-label="Daily trend">
              <h2>Daily trend</h2>
              <div style={{ height: 280 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={data.trend} margin={{ top: 8, right: 12 }}>
                    <CartesianGrid vertical={false} stroke={ink.grid} />
                    <XAxis
                      dataKey="date"
                      tickFormatter={(d: string) => d.slice(5)}
                      stroke={ink.axis}
                      fontSize={12}
                      minTickGap={24}
                    />
                    <YAxis
                      tickFormatter={compact}
                      stroke={ink.axis}
                      fontSize={12}
                      width={56}
                    />
                    <Tooltip formatter={(v) => money(Number(v))} />
                    {/* Legend text stays in ink; the line marker carries the series color. */}
                    <Legend
                      formatter={(value) => (
                        <span style={{ color: ink.text }}>{value}</span>
                      )}
                    />
                    {lines.map((l) => (
                      <Line
                        key={l.key}
                        type="linear"
                        dataKey={l.key}
                        name={l.label}
                        stroke={l.color}
                        strokeWidth={2}
                        strokeDasharray={'dashed' in l ? '6 4' : undefined}
                        dot={data.trend.length <= 14 ? { r: 4 } : false}
                        activeDot={{ r: 5 }}
                        isAnimationActive={false}
                      />
                    ))}
                  </LineChart>
                </ResponsiveContainer>
              </div>
              <TableView
                caption="Daily trend"
                headers={['Date', ...lines.map((l) => l.label)]}
                rows={data.trend.map((p) => [
                  p.date,
                  ...lines.map(
                    (l) => (p[l.key as keyof typeof p] as number | null) ?? 0,
                  ),
                ])}
              />
            </section>
          )}
          <div className="grid gap-6 lg:grid-cols-2">
            {data.expensesByCategory.length > 0 && (
              <Ranked
                title="Operating expenses by category"
                data={data.expensesByCategory}
              />
            )}
            {data.revenueByBranch.length > 1 && (
              <Ranked
                title="Net revenue by branch"
                data={data.revenueByBranch}
              />
            )}
          </div>
        </>
      )}
    </div>
  );
}
