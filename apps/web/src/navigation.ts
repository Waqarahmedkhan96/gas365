import type { Me } from './access-api';

export const pages = [
  {
    id: 'notifications',
    label: 'Notifications',
    description: 'Low stock, overdue credit and open-register alerts.',
    icon: 'notifications',
  },
  {
    id: 'imports',
    label: 'Imports',
    description:
      'Bring customers, suppliers, items and opening balances from Excel or CSV.',
    permission: 'imports.manage',
    icon: 'imports',
  },
  {
    id: 'reports',
    label: 'Reports',
    description: 'Reconciled reports with CSV, Excel and PDF exports.',
    permission: 'reports.view',
    icon: 'reports',
  },
  {
    id: 'udhaar',
    label: 'Udhaar',
    description: 'Credit balances, allocations and aging.',
    permission: 'finance.view',
    icon: 'team',
  },
  {
    id: 'expenses',
    label: 'Expenses',
    description: 'Operating expenses and capital outlays.',
    permission: 'finance.view',
    icon: 'branches',
  },
  {
    id: 'loans',
    label: 'Loans and owner funds',
    description: 'Principal, repayments, investments and withdrawals.',
    permission: 'finance.view',
    icon: 'branches',
  },
  {
    id: 'cash-register',
    label: 'Cash register',
    description: 'Accounts, transfers and controlled drawer closing.',
    permission: 'finance.view',
    icon: 'branches',
  },
  {
    id: 'sales',
    label: 'Sales',
    description: 'Sales, payments, cylinder exchanges and returns.',
    permission: 'sales.view',
    icon: 'branches',
  },
  {
    id: 'customers',
    label: 'Customers',
    description: 'Customer contact details and status.',
    permission: 'customers.view',
    icon: 'team',
  },
  {
    id: 'prices',
    label: 'Price lists',
    description: 'Selling prices by item and sale mode.',
    permission: 'customers.view',
    icon: 'branches',
  },
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
    description: 'Audit timeline and a single view of every correction.',
    permission: 'audit.view',
    icon: 'audit',
  },
  {
    id: 'subscription',
    label: 'Subscription',
    description: 'Your plan, usage, billing history and support access.',
    permission: 'tenant.manage',
    icon: 'subscription',
  },
  {
    id: 'platform',
    label: 'Platform admin',
    description: 'Businesses, plans and owner-approved support views.',
    platformOnly: true,
    icon: 'platform',
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
    (!('ownerOnly' in page) || user.isOwner) &&
    (!('platformOnly' in page) || !!user.platformAdmin)
  );
}
export function pagePath(id: string) {
  return id === 'overview' ? '/workspace' : `/workspace/${id}`;
}
