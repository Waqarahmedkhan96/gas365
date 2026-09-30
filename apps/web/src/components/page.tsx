import { useId, type ReactNode } from 'react';
import { Tabs as TabsPrimitive } from 'radix-ui';
import {
  ArrowLeft,
  CheckCircle2,
  CircleDashed,
  CircleSlash,
  Clock3,
  Inbox,
  Info,
  Loader2,
  Lock,
  Search,
  TriangleAlert,
  XCircle,
  type LucideIcon,
} from 'lucide-react';
import { Button } from './ui/button';
import { PageIcon } from './icons';
import { cn } from '../lib/utils';
import type { Tone } from '../lib/ui-helpers';

export type { Tone };

export function PageHeading({
  title,
  description,
  icon,
  action,
  actionsRef,
}: {
  title: string;
  description: string;
  icon?: string;
  action?: ReactNode;
  actionsRef?: (element: HTMLDivElement | null) => void;
}) {
  return (
    <div className="page-heading">
      <div className="page-heading-title">
        {icon && (
          <span className="page-heading-icon" aria-hidden="true">
            <PageIcon name={icon} size={22} />
          </span>
        )}
        <div>
          <h1>{title}</h1>
          <p className="text-muted-foreground">{description}</p>
        </div>
      </div>
      <div className="page-heading-actions" ref={actionsRef}>
        {action}
      </div>
    </div>
  );
}

const stateIcons: Record<string, LucideIcon> = {
  loading: Loader2,
  error: TriangleAlert,
  denied: Lock,
  empty: Inbox,
};

export function PageState({
  kind,
  title,
  description,
  retry,
  action,
}: {
  kind: 'loading' | 'error' | 'empty' | 'denied';
  title: string;
  description: string;
  retry?: () => void;
  action?: ReactNode;
}) {
  const Icon = stateIcons[kind]!;
  return (
    <section className={`page-state ${kind}`} aria-busy={kind === 'loading'}>
      <div className="state-symbol" aria-hidden="true">
        <Icon size={24} className={kind === 'loading' ? 'spin' : undefined} />
      </div>
      <h2>{title}</h2>
      <p
        role={
          kind === 'error' ? 'alert' : kind === 'loading' ? 'status' : undefined
        }
      >
        {description}
      </p>
      {kind === 'loading' && (
        <div className="skeleton-lines" aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
      )}
      {retry && (
        <Button variant="outline" onClick={retry}>
          Try again
        </Button>
      )}
      {action}
    </section>
  );
}

const toneIcons: Record<Tone, LucideIcon> = {
  neutral: CircleDashed,
  info: Info,
  success: CheckCircle2,
  warning: Clock3,
  danger: XCircle,
};

// Status is always shown as text plus an icon, never colour alone.
export function StatusChip({
  tone = 'neutral',
  children,
  icon,
}: {
  tone?: Tone;
  children: ReactNode;
  icon?: LucideIcon;
}) {
  const Icon = icon || toneIcons[tone];
  return (
    <span className={`status-chip ${tone}`}>
      <Icon aria-hidden="true" size={14} />
      {children}
    </span>
  );
}

export function StatusBadge({
  status,
}: {
  status: 'Draft' | 'Posted' | 'Reversed';
}) {
  const tone: Record<typeof status, Tone> = {
    Draft: 'warning',
    Posted: 'success',
    Reversed: 'danger',
  };
  return (
    <StatusChip
      tone={tone[status]}
      icon={status === 'Reversed' ? CircleSlash : undefined}
    >
      {status}
    </StatusChip>
  );
}

// Summary figure with an icon tile, as used across dashboards, lists and reports.
export function StatCard({
  label,
  value,
  note,
  icon,
  tone = 'neutral',
}: {
  label: string;
  value: ReactNode;
  note?: ReactNode;
  icon?: LucideIcon;
  tone?: Tone;
}) {
  const Icon = icon;
  return (
    <div className={`stat-card ${tone}`}>
      <div className="stat-card-top">
        <span className="stat-card-label">{label}</span>
        {Icon && (
          <span className="stat-card-icon" aria-hidden="true">
            <Icon size={18} />
          </span>
        )}
      </div>
      <strong className="stat-card-value">{value}</strong>
      {note && <span className="stat-card-note">{note}</span>}
    </div>
  );
}

export function StatGrid({
  children,
  label,
}: {
  children: ReactNode;
  label?: string;
}) {
  return (
    <section className="stat-grid" aria-label={label}>
      {children}
    </section>
  );
}

export function SectionTitle({
  children,
  action,
  id,
}: {
  children: ReactNode;
  action?: ReactNode;
  id?: string;
}) {
  return (
    <div className="section-title">
      <h2 id={id}>{children}</h2>
      {action}
    </div>
  );
}

export function Card({
  title,
  icon,
  action,
  children,
  className,
}: {
  title?: ReactNode;
  icon?: LucideIcon;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  const Icon = icon;
  return (
    <section className={cn('card', className)}>
      {(title || action) && (
        <div className="card-header">
          {title && (
            <h3>
              {Icon && <Icon aria-hidden="true" size={18} />}
              {title}
            </h3>
          )}
          {action}
        </div>
      )}
      {children}
    </section>
  );
}

// List toolbar: a labelled search box followed by any filter controls.
export function Toolbar({
  search,
  children,
}: {
  search?: {
    label: string;
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
  };
  children?: ReactNode;
}) {
  const id = useId();
  return (
    <div className="toolbar">
      {search && (
        <div className="search-field">
          <label htmlFor={id} className="sr-only">
            {search.label}
          </label>
          <Search aria-hidden="true" size={18} />
          <input
            id={id}
            type="search"
            value={search.value}
            placeholder={search.placeholder || search.label}
            onChange={(event) => search.onChange(event.target.value)}
          />
        </div>
      )}
      {children}
    </div>
  );
}

export function KeyValues({
  items,
  className,
}: {
  items: [ReactNode, ReactNode][];
  className?: string;
}) {
  return (
    <dl className={cn('kv-list', className)}>
      {items.map(([label, value], index) => (
        <div key={index}>
          <dt>{label}</dt>
          <dd>{value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function BackButton({
  children,
  onClick,
}: {
  children: ReactNode;
  onClick: () => void;
}) {
  return (
    <button type="button" className="back-button" onClick={onClick}>
      <ArrowLeft aria-hidden="true" size={16} />
      {children}
    </button>
  );
}

// Underlined page tabs; only the active panel is mounted.
export function Tabs({
  label,
  value,
  onChange,
  tabs,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  tabs: { id: string; label: string; count?: number; content: ReactNode }[];
}) {
  return (
    <TabsPrimitive.Root
      value={value}
      onValueChange={onChange}
      className="page-tabs"
    >
      <TabsPrimitive.List aria-label={label} className="tab-list">
        {tabs.map((tab) => (
          <TabsPrimitive.Trigger
            key={tab.id}
            value={tab.id}
            className="tab-trigger"
          >
            {tab.label}
            {tab.count !== undefined && (
              <span className="tab-count">{tab.count}</span>
            )}
          </TabsPrimitive.Trigger>
        ))}
      </TabsPrimitive.List>
      {tabs.map((tab) => (
        <TabsPrimitive.Content
          key={tab.id}
          value={tab.id}
          className="tab-panel"
        >
          {tab.content}
        </TabsPrimitive.Content>
      ))}
    </TabsPrimitive.Root>
  );
}

// Footer for server-paged card lists; mirrors the table footer wording.
export function Pager({
  page,
  pageSize,
  total,
  onChange,
}: {
  page: number;
  pageSize: number;
  total: number;
  onChange: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const first = total ? (page - 1) * pageSize + 1 : 0;
  const last = Math.min(total, page * pageSize);
  return (
    <div className="pagination card-pagination">
      <p aria-live="polite">
        Showing {first}–{last} of {total} records · Page {page} of {pages}
      </p>
      <div className="flex gap-2">
        <Button
          variant="outline"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
        >
          Previous
        </Button>
        <Button
          variant="outline"
          disabled={page >= pages}
          onClick={() => onChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  );
}
