import type { ReactNode } from 'react';
import { Button } from './ui/button';
import { Link } from 'react-router-dom';

export function PageHeading({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        <nav aria-label="Breadcrumb" className="breadcrumbs">
          <ol>
            <li>
              <Link to="/workspace">Workspace</Link>
            </li>
            <li aria-hidden="true">/</li>
            <li aria-current="page">{title}</li>
          </ol>
        </nav>
        <h1>{title}</h1>
        <p className="text-muted-foreground">{description}</p>
      </div>
      {action}
    </div>
  );
}

export function PageState({
  kind,
  title,
  description,
  retry,
}: {
  kind: 'loading' | 'error' | 'empty' | 'denied';
  title: string;
  description: string;
  retry?: () => void;
}) {
  return (
    <section className="page-state" aria-busy={kind === 'loading'}>
      <div className="state-symbol" aria-hidden="true">
        {kind === 'loading'
          ? '…'
          : kind === 'error'
            ? '!'
            : kind === 'denied'
              ? '−'
              : '○'}
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
    </section>
  );
}

export function StatusBadge({
  status,
}: {
  status: 'Draft' | 'Posted' | 'Reversed';
}) {
  return (
    <span className={`status-badge ${status.toLowerCase()}`}>{status}</span>
  );
}
