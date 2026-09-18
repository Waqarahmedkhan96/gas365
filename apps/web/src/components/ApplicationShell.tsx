import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import type { Branch, Me, Tenant } from '../access-api';
import { canVisit, pages, pagePath } from '../navigation';
import { Button } from './ui/button';
import { BranchContext } from './branch-context';
import { Input } from './ui/form';
import { Modal, ConfirmButton } from './ui/feedback';

function Mark({ name }: { name: string }) {
  const paths: Record<string, ReactNode> = {
    overview: (
      <>
        <rect x="3" y="3" width="7" height="7" rx="1" />
        <rect x="14" y="3" width="7" height="7" rx="1" />
        <rect x="3" y="14" width="7" height="7" rx="1" />
        <rect x="14" y="14" width="7" height="7" rx="1" />
      </>
    ),
    branches: (
      <>
        <path d="M4 21V8l8-5 8 5v13M2 21h20M9 21v-7h6v7M8 9h1m6 0h1" />
      </>
    ),
    team: (
      <>
        <circle cx="9" cy="8" r="3" />
        <path d="M3 21v-3a6 6 0 0 1 12 0v3M17 5a3 3 0 0 1 0 6m1 4a5 5 0 0 1 3 5" />
      </>
    ),
    roles: (
      <>
        <path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z" />
        <path d="m8 12 3 3 5-6" />
      </>
    ),
    sessions: (
      <>
        <rect x="3" y="4" width="18" height="13" rx="2" />
        <path d="M8 21h8m-4-4v4" />
      </>
    ),
    audit: (
      <>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 6v6l4 3" />
      </>
    ),
    settings: (
      <>
        <path d="M4 7h16M4 17h16" />
        <circle cx="9" cy="7" r="3" />
        <circle cx="16" cy="17" r="3" />
      </>
    ),
    showcase: (
      <>
        <path d="m12 2 10 6-10 6L2 8l10-6Zm-10 11 10 6 10-6M2 18l10 5 10-5" />
      </>
    ),
  };
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      width="20"
      height="20"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {paths[name] || paths.overview}
    </svg>
  );
}

export function ApplicationShell({
  user,
  tenant,
  branches,
  busy,
  onSignOut,
  onSwitch,
  children,
}: {
  user: Me;
  tenant: Tenant;
  branches: Branch[];
  busy: boolean;
  onSignOut: () => void;
  onSwitch: (membershipId: string) => void;
  children: ReactNode;
}) {
  const location = useLocation();
  const main = useRef<HTMLElement>(null);
  const searchInput = useRef<HTMLInputElement>(null);
  const previousPath = useRef(location.pathname);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [query, setQuery] = useState('');
  const available = pages.filter((page) => canVisit(page, user));
  const activeBranches = branches.filter((b) => b.active);
  const [branchId, setBranchId] = useState(() => {
    try {
      return sessionStorage.getItem(`branch:${tenant.tenantId}`) || '';
    } catch {
      return '';
    }
  });
  const selectedBranch =
    activeBranches.find((b) => b.id === branchId)?.id ||
    activeBranches[0]?.id ||
    '';
  useEffect(() => {
    const page = pages.find((p) => pagePath(p.id) === location.pathname);
    document.title = `${page?.label || 'Workspace'} · Gas365`;
    if (previousPath.current !== location.pathname) {
      main.current?.focus();
      previousPath.current = location.pathname;
    }
  }, [location.pathname]);
  useEffect(() => {
    const shortcut = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setSearchOpen((open) => !open);
      }
    };
    window.addEventListener('keydown', shortcut);
    return () => window.removeEventListener('keydown', shortcut);
  }, []);

  function navigation(close?: () => void) {
    return (
      <nav aria-label="Workspace navigation" className="shell-nav">
        <p className="nav-group">WORKSPACE</p>
        {available
          .filter((p) => p.id !== 'showcase')
          .map((page) => (
            <NavLink key={page.id} end to={pagePath(page.id)} onClick={close}>
              <Mark name={page.icon} />
              <span>{page.label}</span>
            </NavLink>
          ))}
        <p className="nav-group mt-6">OPERATIONS</p>
        <p className="nav-help">
          Operational modules will appear here as they become available.
        </p>
        {user.isOwner && (
          <details className="upcoming-nav">
            <summary>Upcoming modules</summary>
            <ul>
              {[
                'Dashboard',
                'Sales',
                'Purchases',
                'Customers',
                'Suppliers',
                'Credit / Udhaar',
                'Cash & banking',
                'Expenses & loans',
                'Deliveries',
                'Reports',
                'Billing',
              ].map((label) => (
                <li key={label}>{label}</li>
              ))}
            </ul>
          </details>
        )}
        {user.isOwner && (
          <>
            <p className="nav-group mt-6">RESOURCES</p>
            <NavLink to="/workspace/showcase" onClick={close}>
              <Mark name="showcase" />
              <span>Component showcase</span>
            </NavLink>
          </>
        )}
      </nav>
    );
  }

  return (
    <div className="application-shell">
      <a className="skip-link" href="#workspace-content">
        Skip to main content
      </a>
      <aside className="desktop-sidebar">
        <Link to="/workspace" className="shell-brand">
          Gas<span>365</span>
          <small>BUSINESS WORKSPACE</small>
        </Link>
        <div className="sidebar-business">
          <span className="business-monogram" aria-hidden="true">
            {tenant.name.slice(0, 1).toUpperCase()}
          </span>
          <div>
            <strong>{tenant.name}</strong>
            <span>{user.isOwner ? 'Owner workspace' : 'Team workspace'}</span>
          </div>
        </div>
        {navigation()}
        <div className="sidebar-footer">
          Built around your business.
          <br />
          <Link to="/">Platform status</Link>
        </div>
      </aside>
      <div className="shell-body">
        <div className="mobile-topline">
          <Link to="/workspace">
            Gas<span>365</span>
          </Link>
          <strong>{tenant.name}</strong>
        </div>
        <header className="shell-header">
          <div className="mobile-menu">
            <Modal
              title="Your workspace"
              description={tenant.name}
              open={mobileOpen}
              onOpenChange={setMobileOpen}
              drawer
              trigger={
                <Button
                  variant="outline"
                  size="icon"
                  aria-label="Open navigation"
                >
                  ☰
                </Button>
              }
            >
              {navigation(() => setMobileOpen(false))}
            </Modal>
          </div>
          <label className="branch-picker">
            Active branch
            <select
              aria-describedby="branch-context-note"
              value={selectedBranch}
              disabled={!activeBranches.length}
              onChange={(event) => {
                setBranchId(event.target.value);
                try {
                  sessionStorage.setItem(
                    `branch:${tenant.tenantId}`,
                    event.target.value,
                  );
                } catch {
                  /* Selection remains available in memory. */
                }
              }}
            >
              {activeBranches.length ? (
                activeBranches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))
              ) : (
                <option value="">No assigned branches</option>
              )}
            </select>
          </label>
          <div className="shell-header-actions">
            <Modal
              title="Go to a page"
              onOpenAutoFocus={(event) => {
                event.preventDefault();
                searchInput.current?.focus();
              }}
              description="Search the pages you can access. Business record search will be added with operational modules."
              open={searchOpen}
              onOpenChange={setSearchOpen}
              trigger={
                <Button variant="outline" aria-label="Search pages">
                  <span aria-hidden="true">⌕</span>
                  <span className="search-label">Search pages</span>
                  <kbd className="search-label">Ctrl K</kbd>
                </Button>
              }
            >
              <label htmlFor="page-search" className="sr-only">
                Find a page
              </label>
              <Input
                id="page-search"
                ref={searchInput}
                placeholder="Search pages…"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
              <ul className="command-results">
                {available
                  .filter((page) =>
                    page.label.toLowerCase().includes(query.toLowerCase()),
                  )
                  .map((page) => (
                    <li key={page.id}>
                      <Link
                        to={pagePath(page.id)}
                        onClick={() => {
                          setSearchOpen(false);
                          setQuery('');
                        }}
                      >
                        <Mark name={page.icon} />
                        {page.label}
                        <span aria-hidden="true">↗</span>
                      </Link>
                    </li>
                  ))}
              </ul>
              {!available.some((page) =>
                page.label.toLowerCase().includes(query.toLowerCase()),
              ) && (
                <p role="status" className="py-6 text-muted-foreground">
                  No matching pages. Try another name.
                </p>
              )}
            </Modal>
            <span className="user-name">{user.name}</span>
            <ConfirmButton
              title="Sign out of Gas365?"
              description="This session will end. You can sign in again whenever you need."
              onConfirm={onSignOut}
              disabled={busy}
            >
              Sign out
            </ConfirmButton>
          </div>
        </header>
        {user.workspaces.length > 1 && (
          <div className="workspace-switch">
            <label>
              Workspace
              <select
                value={user.membershipId}
                disabled={busy}
                onChange={(e) => onSwitch(e.target.value)}
              >
                {user.workspaces.map((w) => (
                  <option key={w.membershipId} value={w.membershipId}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
        )}
        <main
          id="workspace-content"
          ref={main}
          tabIndex={-1}
          className="shell-main"
        >
          <BranchContext.Provider value={selectedBranch}>
            {children}
          </BranchContext.Provider>
        </main>
        <footer className="shell-footer">
          <span id="branch-context-note">
            Inventory follows the active branch. Business settings apply to the
            whole workspace.
          </span>
          <span>{tenant.timeZone} · PKR</span>
        </footer>
      </div>
    </div>
  );
}
