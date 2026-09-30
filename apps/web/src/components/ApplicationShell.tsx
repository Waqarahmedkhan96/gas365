import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { DropdownMenu } from 'radix-ui';
import {
  Building2,
  ChevronDown,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  Search,
} from 'lucide-react';
import type { Branch, Me, Tenant } from '../access-api';
import {
  canVisit,
  groupOrder,
  pageGroups,
  pages,
  pagePath,
} from '../navigation';
import { Button } from './ui/button';
import { BranchContext } from './branch-context';
import { Input } from './ui/form';
import { ConfirmDialog, Modal } from './ui/feedback';
import { BrandMark, PageIcon } from './icons';
import { NotificationBell } from '../NotificationsPage';

const collapsedKey = 'gas365:sidebar-collapsed';

function initials(name: string) {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('') || '?'
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
  const [signOutOpen, setSignOutOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(collapsedKey) === '1';
    } catch {
      return false;
    }
  });
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
  const can = (permission: string) => user.permissions.includes(permission);
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

  function toggleSidebar() {
    setCollapsed((value) => {
      try {
        localStorage.setItem(collapsedKey, value ? '0' : '1');
      } catch {
        /* The preference stays in memory for this visit. */
      }
      return !value;
    });
  }

  function navigation(close?: () => void) {
    const order = (id: string, group: string) => {
      const index = groupOrder[group]?.indexOf(id) ?? -1;
      return index < 0 ? 99 : index;
    };
    return (
      <nav aria-label="Workspace navigation" className="shell-nav">
        {pageGroups.map((group) => {
          const items = available
            .filter((page) => page.group === group.id)
            .sort((a, b) => order(a.id, group.id) - order(b.id, group.id));
          if (!items.length) return null;
          const headingId = `nav-group-${group.id}${close ? '-drawer' : ''}`;
          return (
            <div key={group.id} className="nav-section">
              <p className="nav-group" id={headingId}>
                {group.label}
              </p>
              <ul aria-labelledby={headingId}>
                {items.map((page) => (
                  <li key={page.id}>
                    <NavLink
                      end
                      to={pagePath(page.id)}
                      onClick={close}
                      title={collapsed && !close ? page.label : undefined}
                    >
                      <PageIcon name={page.icon} />
                      <span className="nav-label">{page.label}</span>
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
      </nav>
    );
  }

  return (
    <div
      className="application-shell"
      data-sidebar={collapsed ? 'collapsed' : 'expanded'}
    >
      <a className="skip-link" href="#workspace-content">
        Skip to main content
      </a>
      <aside className="desktop-sidebar" id="workspace-sidebar">
        <Link to="/workspace" className="shell-brand" aria-label="Gas365 home">
          <BrandMark />
          <span className="brand-word" aria-hidden="true">
            Gas<span>365</span>
          </span>
        </Link>
        {navigation()}
        <div className="sidebar-business">
          <span className="business-monogram" aria-hidden="true">
            {tenant.name.slice(0, 1).toUpperCase()}
          </span>
          <div className="sidebar-business-text">
            <strong>{tenant.name}</strong>
            <span>{user.isOwner ? 'Owner workspace' : 'Team workspace'}</span>
          </div>
        </div>
      </aside>
      <div className="shell-body">
        <header className="shell-header">
          <div className="mobile-menu">
            <Modal
              title="Gas365"
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
                  <Menu aria-hidden="true" size={20} />
                </Button>
              }
            >
              {navigation(() => setMobileOpen(false))}
            </Modal>
          </div>
          <Button
            variant="ghost"
            size="icon"
            className="sidebar-toggle"
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            aria-controls="workspace-sidebar"
            aria-expanded={!collapsed}
            onClick={toggleSidebar}
          >
            {collapsed ? (
              <PanelLeftOpen aria-hidden="true" size={20} />
            ) : (
              <PanelLeftClose aria-hidden="true" size={20} />
            )}
          </Button>
          <div className="header-business">
            <span className="header-business-icon" aria-hidden="true">
              <Building2 size={20} />
            </span>
            <div>
              <strong>{tenant.name}</strong>
              <span>{tenant.timeZone} · PKR</span>
            </div>
          </div>
          <div className="shell-header-actions">
            <label className="branch-picker">
              <span>Active branch</span>
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
            <Modal
              title="Go to a page"
              onOpenAutoFocus={(event) => {
                event.preventDefault();
                searchInput.current?.focus();
              }}
              description="Search the pages you can access."
              open={searchOpen}
              onOpenChange={setSearchOpen}
              trigger={
                <Button
                  variant="outline"
                  className="search-trigger"
                  aria-label="Search pages"
                >
                  <Search aria-hidden="true" size={18} />
                  <span className="search-label" aria-hidden="true">
                    Search
                  </span>
                  <kbd className="search-label" aria-hidden="true">
                    Ctrl K
                  </kbd>
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
                        <PageIcon name={page.icon} />
                        {page.label}
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
            <NotificationBell user={user} />
            <DropdownMenu.Root>
              <DropdownMenu.Trigger asChild>
                <button
                  type="button"
                  className="account-trigger"
                  aria-label={`${user.name}, account menu`}
                  disabled={busy}
                >
                  <span className="avatar" aria-hidden="true">
                    {initials(user.name)}
                  </span>
                  <span className="account-name" aria-hidden="true">
                    {user.name}
                  </span>
                  <ChevronDown aria-hidden="true" size={16} />
                </button>
              </DropdownMenu.Trigger>
              <DropdownMenu.Portal>
                <DropdownMenu.Content
                  className="menu-panel"
                  align="end"
                  sideOffset={8}
                >
                  <DropdownMenu.Label className="menu-identity">
                    <strong>{user.name}</strong>
                    <span>{user.email}</span>
                  </DropdownMenu.Label>
                  <DropdownMenu.Separator className="menu-separator" />
                  <DropdownMenu.Item asChild className="menu-item">
                    <Link to="/workspace/sessions">
                      <PageIcon name="sessions" /> Sessions
                    </Link>
                  </DropdownMenu.Item>
                  {can('tenant.manage') && (
                    <>
                      <DropdownMenu.Item asChild className="menu-item">
                        <Link to="/workspace/subscription">
                          <PageIcon name="subscription" /> Subscription
                        </Link>
                      </DropdownMenu.Item>
                      <DropdownMenu.Item asChild className="menu-item">
                        <Link to="/workspace/settings">
                          <PageIcon name="settings" /> Business settings
                        </Link>
                      </DropdownMenu.Item>
                    </>
                  )}
                  <DropdownMenu.Separator className="menu-separator" />
                  <DropdownMenu.Item
                    className="menu-item danger"
                    onSelect={() => setSignOutOpen(true)}
                  >
                    <LogOut aria-hidden="true" size={18} /> Sign out
                  </DropdownMenu.Item>
                </DropdownMenu.Content>
              </DropdownMenu.Portal>
            </DropdownMenu.Root>
            <ConfirmDialog
              title="Sign out of Gas365?"
              description="This session will end. You can sign in again whenever you need."
              open={signOutOpen}
              onOpenChange={setSignOutOpen}
              onConfirm={onSignOut}
            />
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
          <span>Gas365</span>
        </footer>
      </div>
    </div>
  );
}
