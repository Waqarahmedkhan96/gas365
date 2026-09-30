import type { Me } from './access-api';

export const pages = [
  {
    id: 'notifications',
    label: 'Notifications',
    description: 'Low stock, overdue credit and open-register alerts.',
    icon: 'notifications',
    group: 'home',
  },
  {
    id: 'imports',
    label: 'Imports',
    description:
      'Bring customers, suppliers, items and opening balances from Excel or CSV.',
    permission: 'imports.manage',
    icon: 'imports',
    group: 'business',
  },
  {
    id: 'reports',
    label: 'Reports',
    description: 'Reconciled reports with CSV, Excel and PDF exports.',
    permission: 'reports.view',
    icon: 'reports',
    group: 'reports',
  },
  {
    id: 'udhaar',
    label: 'Udhaar',
    description: 'Credit balances, allocations and aging.',
    permission: 'finance.view',
    icon: 'udhaar',
    group: 'money',
  },
  {
    id: 'expenses',
    label: 'Expenses',
    description: 'Operating expenses and capital outlays.',
    permission: 'finance.view',
    icon: 'expenses',
    group: 'money',
  },
  {
    id: 'loans',
    label: 'Loans and owner funds',
    description: 'Principal, repayments, investments and withdrawals.',
    permission: 'finance.view',
    icon: 'loans',
    group: 'money',
  },
  {
    id: 'cash-register',
    label: 'Cash register',
    description: 'Accounts, transfers and controlled drawer closing.',
    permission: 'finance.view',
    icon: 'cash-register',
    group: 'money',
  },
  {
    id: 'sales',
    label: 'Sales',
    description: 'Sales, payments, cylinder exchanges and returns.',
    permission: 'sales.view',
    icon: 'sales',
    group: 'sales',
  },
  {
    id: 'customers',
    label: 'Customers',
    description: 'Customer contact details and status.',
    permission: 'customers.view',
    icon: 'customers',
    group: 'sales',
  },
  {
    id: 'prices',
    label: 'Price lists',
    description: 'Selling prices by item and sale mode.',
    permission: 'customers.view',
    icon: 'prices',
    group: 'sales',
  },
  {
    id: 'purchases',
    label: 'Purchases',
    description: 'Purchase drafts, receipts and payments.',
    permission: 'purchases.view_cost',
    icon: 'purchases',
    group: 'stock',
  },
  {
    id: 'suppliers',
    label: 'Suppliers',
    description: 'Supplier profiles and branch ledgers.',
    permission: 'purchases.view_cost',
    icon: 'suppliers',
    group: 'stock',
  },
  {
    id: 'inventory',
    label: 'Inventory',
    description: 'Catalog, stock balances and movement history.',
    permission: 'inventory.view',
    icon: 'inventory',
    group: 'stock',
  },
  {
    id: 'overview',
    label: 'Overview',
    description: 'Your business, branches and team at a glance.',
    icon: 'overview',
    group: 'home',
  },
  {
    id: 'branches',
    label: 'Branches',
    description: 'Manage locations and keep branch access clear.',
    icon: 'branches',
    group: 'business',
  },
  {
    id: 'team',
    label: 'Team',
    description: 'Invite colleagues and manage their access.',
    permission: 'users.manage',
    icon: 'team',
    group: 'business',
  },
  {
    id: 'roles',
    label: 'Roles',
    description: 'Define the permissions available to your team.',
    permission: 'users.manage',
    icon: 'roles',
    group: 'business',
  },
  {
    id: 'settings',
    label: 'Business settings',
    description: 'Business details and time zone.',
    permission: 'tenant.manage',
    icon: 'settings',
    group: 'business',
  },
  {
    id: 'sessions',
    label: 'Sessions',
    description: 'Review and end your active sessions.',
    icon: 'sessions',
    group: 'business',
  },
  {
    id: 'audit',
    label: 'Activity',
    description: 'Audit timeline and a single view of every correction.',
    permission: 'audit.view',
    icon: 'audit',
    group: 'reports',
  },
  {
    id: 'subscription',
    label: 'Subscription',
    description: 'Your plan, usage, billing history and support access.',
    permission: 'tenant.manage',
    icon: 'subscription',
    group: 'business',
  },
  {
    id: 'platform',
    label: 'Platform admin',
    description: 'Businesses, plans and owner-approved support views.',
    platformOnly: true,
    icon: 'platform',
    group: 'platform',
  },
  {
    id: 'showcase',
    label: 'Component showcase',
    description: 'Explore the Gas365 interface patterns with sample data.',
    ownerOnly: true,
    icon: 'showcase',
    group: 'resources',
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

// Sidebar sections, in display order. Pages keep their order within a section.
export const pageGroups = [
  { id: 'home', label: 'Home' },
  { id: 'sales', label: 'Sales' },
  { id: 'stock', label: 'Stock and purchasing' },
  { id: 'money', label: 'Money' },
  { id: 'reports', label: 'Reports' },
  { id: 'business', label: 'Business' },
  { id: 'platform', label: 'Platform' },
  { id: 'resources', label: 'Resources' },
] as const;
export const groupOrder: Record<string, string[]> = {
  home: ['overview', 'notifications'],
  sales: ['sales', 'customers', 'prices'],
  stock: ['inventory', 'purchases', 'suppliers'],
  money: ['cash-register', 'expenses', 'udhaar', 'loans'],
  reports: ['reports', 'audit'],
  business: [
    'branches',
    'team',
    'roles',
    'settings',
    'imports',
    'subscription',
    'sessions',
  ],
};
