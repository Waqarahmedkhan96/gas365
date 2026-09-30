import { useId, type ReactNode } from 'react';
import {
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

export function PageHeading({
  title,
  description,
  icon,
  action,
}: {
  title: string;
  description: string;
  icon?: string;
  action?: ReactNode;
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
      {action && <div className="page-heading-actions">{action}</div>}
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

export type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';
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
