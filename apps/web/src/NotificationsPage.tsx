import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import type { Notification } from './operations-types';

const targets: Record<Notification['kind'], string> = {
  LowStock: '/workspace/inventory',
  OverdueCredit: '/workspace/udhaar',
  RegisterOpen: '/workspace/cash-register',
};
const root = '/api/workspace/notifications';

// Header badge; polls the unread count so new alerts appear without a reload.
export function NotificationBell({ user }: { user: Me }) {
  const summary = useQuery({
    queryKey: ['notifications', user.tenantId, 'summary'],
    queryFn: () =>
      api<{ unread: number }>(
        root + '/summary',
        'GET',
        undefined,
        user.tenantId,
      ),
    refetchInterval: 60_000,
  });
  const unread = summary.data?.unread ?? 0;
  return (
    <Link
      to="/workspace/notifications"
      className="notification-bell"
      aria-label={`Notifications, ${unread} unread`}
    >
      <Bell aria-hidden="true" size={20} />
      {unread > 0 && (
        <span className="notification-count" aria-hidden="true">
          {unread > 99 ? '99+' : unread}
        </span>
      )}
    </Link>
  );
}

export function NotificationsPage({
  user,
  timeZone,
}: {
  user: Me;
  timeZone: string;
}) {
  const cache = useQueryClient();
  const [page, setPage] = useState(1);
  const [error, setError] = useState('');
  const list = useQuery({
    queryKey: ['notifications', user.tenantId, page],
    queryFn: () =>
      api<{ items: Notification[]; total: number; pageSize: number }>(
        `${root}?page=${page}`,
        'GET',
        undefined,
        user.tenantId,
      ),
  });
  async function mark(path: string) {
    setError('');
    try {
      await api(root + path, 'POST', undefined, user.tenantId);
      await cache.invalidateQueries({
        queryKey: ['notifications', user.tenantId],
      });
    } catch (e) {
      setError((e as Error).message);
    }
  }
  const items = list.data?.items ?? [];
  return (
    <section className="panel grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="section-help">
          Alerts clear themselves when the condition is resolved: stock is
          replenished, credit is collected or the register is closed.
        </p>
        {items.some((n) => !n.read) && (
          <Button variant="secondary" onClick={() => void mark('/read-all')}>
            Mark all as read
          </Button>
        )}
      </div>
      {error && <p role="alert">{error}</p>}
      {list.error && <p role="alert">{list.error.message}</p>}
      {list.isPending && <p role="status">Loading notifications…</p>}
      {list.data && items.length === 0 && (
        <p>No open alerts. Everything needing attention will appear here.</p>
      )}
      <ul className="grid gap-3">
        {items.map((n) => (
          <li key={n.id} className="record-row">
            <div className="grid gap-1">
              <strong>
                {n.read ? '' : 'New · '}
                {n.title}
              </strong>
              <span>{n.body}</span>
              <time dateTime={n.createdAt} className="section-help">
                Raised{' '}
                {new Date(n.createdAt).toLocaleString('en-PK', { timeZone })}
              </time>
            </div>
            <div className="flex flex-wrap gap-2">
              <Link to={targets[n.kind]}>Open</Link>
              {!n.read && (
                <Button
                  variant="secondary"
                  onClick={() => void mark(`/${n.id}/read`)}
                >
                  Mark as read
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>
      {list.data && list.data.total > list.data.pageSize && (
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
            disabled={page * list.data.pageSize >= list.data.total}
            onClick={() => setPage(page + 1)}
          >
            Next
          </Button>
        </div>
      )}
    </section>
  );
}
