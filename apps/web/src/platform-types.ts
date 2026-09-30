export type Plan = {
  id: string;
  code: string;
  name: string;
  monthlyPrice: number | null;
  maxBranches: number;
  maxUsers: number;
  maxAdmins: number;
  active: boolean;
  version: number;
  // Calculated by the server: ten monthly payments for twelve months.
  yearlyPrice: number | null;
  yearlySaving: number | null;
};
export type BillingPeriod = 'Monthly' | 'Yearly';
export type Usage = {
  month: string;
  branches: number;
  users: number;
  admins: number;
  salesInvoices: number;
  purchaseInvoices: number;
  financeEntries: number;
  stockDocuments: number;
  exports: number;
  importedRows: number;
  storageBytes: number;
};
export type Subscription = {
  plan: Plan | null;
  status: 'Trial' | 'Active' | 'Expired';
  trialEndsAt: string | null;
  paidThrough: string | null;
  requestedPlan: Plan | null;
  usage: Usage;
  plans: Plan[];
  history: {
    id: string;
    kind: string;
    planName: string | null;
    amount: number | null;
    reference: string;
    note: string;
    periodEnd: string | null;
    occurredAt: string;
  }[];
  paymentProvider: string;
  requestedBillingPeriod: BillingPeriod | null;
};
export type SupportGrant = {
  id: string;
  tenantId: string;
  tenantName: string;
  includeFinance: boolean;
  reason: string;
  createdAt: string;
  expiresAt: string;
  active: boolean;
};
export type Onboarding = {
  dismissed: boolean;
  steps: { key: string; label: string; page: string; done: boolean }[];
};
export type TenantSummary = {
  tenantId: string;
  name: string;
  plan: string | null;
  status: 'Trial' | 'Active' | 'Expired';
  trialEndsAt: string | null;
  paidThrough: string | null;
  requestedPlan: string | null;
  users: number;
  branches: number;
  transactionsThisMonth: number;
  createdAt: string;
  requestedBillingPeriod: BillingPeriod | null;
};

export const daysLeft = (iso: string) =>
  Math.max(0, Math.ceil((new Date(iso).getTime() - Date.now()) / 86_400_000));
export const dateText = (iso: string | null, timeZone: string) =>
  iso
    ? new Date(iso).toLocaleDateString('en-PK', {
        timeZone,
        dateStyle: 'medium',
      })
    : '—';
