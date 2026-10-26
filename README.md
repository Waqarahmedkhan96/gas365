# Gas365

Gas365 is a multi-tenant SaaS platform for LPG retailers and distributors. One business can run several branches from one place: gas and cylinder stock, purchasing, sales, customer credit, cash and reports. The whole app is available in English and Urdu.

## What it handles

| Area | What the app does |
|---|---|
| **Businesses, branches and staff** | Each business has its own isolated workspace, branches and team. Roles and permissions decide what each person can see and do, branch by branch. |
| **Catalog and inventory** | LPG by kilogram, cylinders and spare parts across several warehouses. Stock moves, transfers and adjustments are recorded in a ledger with weighted-average costing. |
| **Purchasing** | Suppliers, multi-line purchase receipts, transport costs spread across the goods, supplier payments and supplier balances, with PDF vouchers. |
| **Sales and cylinders** | Price lists, refills, full cylinders, exchanges and cylinder loans with deposits. Every customer cylinder is tracked, and every sale gets a receipt. |
| **Credit (udhaar), cash and expenses** | Customer and supplier balances, overdue credit, daily cash-register sessions, bank accounts, expenses, owner funds and loans. |
| **Reports and dashboard** | Sales, profit, stock value, credit aging, supplier payables and cylinder custody. Every report can be exported to CSV, Excel or PDF, and every total ties back to the underlying records. |
| **Imports and audit** | Excel and CSV imports for opening stock and balances, in-app alerts, a full audit timeline, and corrections made by reversal instead of editing history. |
| **Subscriptions** | Three plans with branch and user limits, a free trial, monthly or yearly billing, and owner-approved support access. |

## How it is built

- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS, TanStack Query and React Router. Responsive on desktop and mobile, with a right-to-left Urdu layout.
- **Backend:** ASP.NET Core Web API (.NET 10) with Entity Framework Core, built as a modular monolith (Domain, Application, Infrastructure and API layers).
- **Database:** PostgreSQL. Every business's data is separated by tenant, and the server calculates every amount.
- **Security:** cookie-based sign-in with CSRF protection, Google sign-in, permission checks on every request, and an audit log of every change.
- **Testing:** xUnit unit, integration and architecture tests, and Playwright end-to-end tests.
- **DevOps:** Docker Compose, GitHub Actions CI, and Caddy for HTTPS.

## Run it locally

You need Docker and Node.js. Run:

```bash
node scripts/bootstrap.mjs
```

Then open [localhost:8080](http://localhost:8080). The script creates local credentials and starts the database, API and web app. See [local development](docs/LOCAL_DEVELOPMENT.md) for tests, hot reload and database migrations.

## Repository layout

```
apps/web/     React frontend
src/          ASP.NET Core API, application, domain and infrastructure layers
tests/        Unit, integration, architecture and end-to-end tests
deploy/       Production Docker Compose and deployment files
docs/         Technical documentation
```

## Author

Built by [Waqar Ahmed Khan](https://github.com/Waqarahmedkhan96).
