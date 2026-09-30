import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  Bell,
  CheckCheck,
  HandCoins,
  PackageX,
  Wallet,
  type LucideIcon,
} from 'lucide-react';
import { PageState } from './components/page';
import { PageActions } from './components/page-actions';
import { api, type Me } from './access-api';
import { Button } from './components/ui/button';
import type { Notification } from './operations-types';
import { tr } from './i18n';

const targets: Record<Notification['kind'], string> = {
  LowStock: '/workspace/inventory',
  OverdueCredit: '/workspace/udhaar',
  RegisterOpen: '/workspace/cash-register',
};
const icons: Record<Notification['kind'], LucideIcon> = {
  LowStock: PackageX,
  OverdueCredit: HandCoins,
  RegisterOpen: Wallet,
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
    <div className="grid gap-6">
      {items.some((n) => !n.read) && (
        <PageActions>
          <Button variant="outline" onClick={() => void mark('/read-all')}>
            <CheckCheck aria-hidden="true" size={18} /> {tr('Mark all as read')}
          </Button>
        </PageActions>
      )}
      <p className="section-help">
        {tr(
          'Alerts clear themselves when the condition is resolved: stock is replenished, credit is collected or the register is closed.',
        )}
      </p>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {list.error && (
        <p role="alert" className="error-banner">
          {list.error.message}
        </p>
      )}
      {list.isPending && <p role="status">{tr('Loading notifications…')}</p>}
      {list.data && items.length === 0 && (
        <PageState
          kind="empty"
          title={tr('You are all caught up')}
          description={tr(
            'No open alerts. Everything needing attention will appear here.',
          )}
        />
      )}
      {items.length > 0 && (
        <ul className="alert-list">
          {items.map((n) => {
            const Icon = icons[n.kind];
            return (
              <li key={n.id} className={n.read ? 'read' : 'unread'}>
                <span className={`alert-icon ${n.kind}`} aria-hidden="true">
                  <Icon size={20} />
                </span>
                <div className="alert-body">
                  <strong>
                    {n.read ? '' : tr('New · ')}
                    {n.title}
                  </strong>
                  <span>{n.body}</span>
                  <time dateTime={n.createdAt}>
                    {tr('Raised')}{' '}
                    {new Date(n.createdAt).toLocaleString('en-PK', {
                      timeZone,
                    })}
                  </time>
                </div>
                <div className="alert-actions">
                  <Button variant="outline" asChild>
                    <Link to={targets[n.kind]}>{tr('Open')}</Link>
                  </Button>
                  {!n.read && (
                    <Button
                      variant="ghost"
                      onClick={() => void mark(`/${n.id}/read`)}
                    >
                      {tr('Mark as read')}
                    </Button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
      {list.data && list.data.total > list.data.pageSize && (
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
            disabled={page * list.data.pageSize >= list.data.total}
            onClick={() => setPage(page + 1)}
          >
            {tr('Next')}
          </Button>
        </div>
      )}
    </div>
  );
}
