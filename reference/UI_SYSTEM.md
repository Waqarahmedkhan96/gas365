# Gas365 UI System

## Direction

For the first implementation, use the same general visual family observed in the GasFlow screenshots: white and pale-grey surfaces, dark navy navigation, muted blue primary actions, compact cards, rounded borders and restrained shadows. Do not copy its logo, exact text, images or component code.

## Tokens

```css
--navy-950: #081E31;
--navy-900: #0D2A45;
--navy-800: #12385A;
--blue-600: #1F6FA8;
--blue-500: #2F80B9;
--blue-100: #DCEEF9;
--page: #F7F9FC;
--surface: #FFFFFF;
--border: #E4EAF0;
--text: #1C2B3A;
--muted: #667788;
--success: #17A673;
--warning: #F3A712;
--danger: #D64545;
```

Use Inter or a metrically stable open font. Standard radius 8–10 px, card radius 12 px, 4/8/12/16/24/32 spacing scale. Keep dense operational tables readable; do not inflate every element.

## Navigation

Primary groups:

- Dashboard
- Sales
- Purchases
- Inventory
  - LPG Stock
  - Cylinders
  - Spare Parts
  - Transfers/Adjustments
- Customers
- Suppliers
- Credit/Udhaar
- Cash & Banking
- Expenses & Loans
- Deliveries
- Reports
- Team & Permissions
- Branches
- Settings/Billing

Items are permission-filtered but routes remain API-protected.

## Core page pattern

- Breadcrumb/title and one dominant action.
- Compact KPI cards only when they help the task.
- Search/filter bar with saved filters later.
- Server-paginated TanStack Table.
- Detail drawer for quick inspection; page for complex documents.
- Explicit draft/posted/reversed status.
- Empty, loading, error and permission-denied states.
- Mobile: collapsible navigation, stacked summaries and card/list alternative for wide tables.

## Key workflows/screens

1. Tenant onboarding and business/branch setup
2. Dashboard with period and branch filters
3. Stock directory and stock detail ledger
4. LPG/cylinder/spare-part creation
5. Purchase list, create, details and voucher
6. Sales list, create, details and receipt
7. Customer list, profile, ledger and payment
8. Supplier list, profile, ledger and payment
9. Credit/Udhaar aging
10. Expense and loan registers
11. Cash register open/close/history
12. Inventory transfer/adjustment/reversal
13. Reports and exports
14. Team, roles and permission matrix
15. Subscription and tenant settings

## Charts

Use Recharts in React. Server returns aggregated report data. Charts require table/summary alternatives and must not become authoritative calculation engines. Avoid Python for normal charts.
