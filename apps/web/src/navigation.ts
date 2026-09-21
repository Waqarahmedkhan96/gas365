import type { Me } from './access-api';

export const pages = [
  {
    id: 'purchases',
    label: 'Purchases',
    description: 'Purchase drafts, receipts and payments.',
    permission: 'purchases.view_cost',
    icon: 'branches',
  },
  {
    id: 'suppliers',
    label: 'Suppliers',
    description: 'Supplier profiles and branch ledgers.',
    permission: 'purchases.view_cost',
    icon: 'team',
  },
  {
    id: 'inventory',
    label: 'Inventory',
    description: 'Catalog, stock balances and movement history.',
    permission: 'inventory.view',
    icon: 'branches',
  },
  {
    id: 'overview',
    label: 'Overview',
    description: 'Your business, branches and team at a glance.',
    icon: 'overview',
  },
  {
    id: 'branches',
    label: 'Branches',
    description: 'Manage locations and keep branch access clear.',
    icon: 'branches',
  },
  {
    id: 'team',
    label: 'Team',
    description: 'Invite colleagues and manage their access.',
    permission: 'users.manage',
    icon: 'team',
  },
  {
    id: 'roles',
    label: 'Roles',
    description: 'Define the permissions available to your team.',
    permission: 'users.manage',
    icon: 'roles',
  },
  {
    id: 'settings',
    label: 'Business settings',
    description: 'Business details and time zone.',
    permission: 'tenant.manage',
    icon: 'settings',
  },
  {
    id: 'sessions',
    label: 'Sessions',
    description: 'Review and end your active sessions.',
    icon: 'sessions',
  },
  {
    id: 'audit',
    label: 'Activity',
    description: 'Recent changes in your workspace.',
    permission: 'audit.view',
    icon: 'audit',
  },
  {
    id: 'showcase',
    label: 'Component showcase',
    description: 'Explore the Gas365 interface patterns with sample data.',
    ownerOnly: true,
    icon: 'showcase',
  },
] as const;
export function canVisit(page: (typeof pages)[number], user: Me) {
  return (
    (!('permission' in page) || user.permissions.includes(page.permission)) &&
    (!('ownerOnly' in page) || user.isOwner)
  );
}
export function pagePath(id: string) {
  return id === 'overview' ? '/workspace' : `/workspace/${id}`;
}
