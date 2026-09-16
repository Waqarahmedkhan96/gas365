# Source Review

## Royal LPG workbook

The workbook contains 47 worksheets and a large formula-driven reporting layer. Major areas include:

- Sales Overview
- Expenses Overview
- Loan Overview and LOANS
- Major Expenses
- Spare Parts Purchase
- Spare Parts Inventory & Profit
- `_SP_FIFO`
- LPG Purchases and LPG Profit
- Stock Adjustments
- Thirty daily sheets (`Sep 01`–`Sep 30`)
- Weekly Summary and Monthly Summary
- Inventory
- Guide
- `_ReportData`

The workbook demonstrates real requirements: daily sales, LPG by weight/type, cylinder identifiers, payment modes, paid/balance amounts, shop and major expenses, loans, stock adjustments, purchase tracking, spare-parts COGS/profit, consolidated reports and opening/closing cash.

### What should migrate into Gas365

- Daily operational entry, but as database transactions rather than a sheet per day.
- Separate LPG, cylinder and spare-parts concepts.
- Sales type and payment mode.
- Paid amount and outstanding balance.
- Purchases, expenses, loans and adjustments.
- Weighted/FIFO-ready costing, inventory valuation and gross profit.
- Daily/weekly/monthly views and filterable master registers.
- Opening and closing cash reconciliation.

### Workbook weaknesses not to reproduce

- A separate worksheet per date is not scalable.
- Thousands of repeated formulas make maintenance and auditing fragile.
- Reports and calculation helpers are tightly coupled.
- Editable cells and calculated records can be confused.
- Corrections may overwrite history rather than form an explicit reversal trail.
- Workbook access control is not sufficient for multi-company SaaS isolation.

The workbook is a requirements source, not a database schema.

## GasFlow screenshot archive

The archive contains 142 screenshots covering:

- Login, registration and subscription selection
- Dashboard
- Business profile, branches and team management
- Cylinder refill calculator
- Stock directory and LPG stock configuration
- Spare parts and categories
- Purchases, purchase details and purchase vouchers
- Sales, sales details and receipts
- Customers and customer sales history
- Expenses
- Credit/Udhaar book and customer profile
- Cash register and reconciliation history
- Suppliers and supplier purchase history
- Business, P&L and spare-parts reports
- Account settings and billing

### Useful patterns

- Stable left navigation and compact top context bar
- Summary cards above searchable registers
- Drawer/modal forms for short creation tasks
- Dedicated full-page forms for multi-line sales and purchases
- Detail pages with financial summary cards
- Printable receipts/vouchers
- Branch selector and role-aware navigation

### Patterns to improve

- Some screens spread critical data over excessive whitespace.
- Form validation and accounting invariants are not visible from screenshots.
- Several modules appear CRUD-first rather than ledger-first.
- The design needs stronger responsive/mobile specifications.
- Destructive corrections should use explicit reversal workflows.
- Reports need reproducible definitions and reconciliation back to ledgers.

## Public technical observations

The public GasFlow application is rendered with Next.js/React and is fronted by Cloudflare/Netlify. Its public metadata advertises multi-branch stock, sales, purchases, credit, staff roles, reporting, offline operation and cloud backups. These claims are inspiration only and must not be treated as verified internal behaviour.
