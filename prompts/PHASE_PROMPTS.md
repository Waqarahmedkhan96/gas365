# Gas365 Controlled Phase Prompts

Run exactly one prompt per development session unless the active prompt explicitly says otherwise.

## Prompt 00 — Repository Bootstrap and Source Inventory

Read `AGENTS.md`, `PROJECT_STATE.md`, `00_MASTER_PROMPT.md` and all `reference/*.md`. Inspect all files already in the repository, including the workbook and screenshot archive under `reference/inputs/` if present. Do not implement product modules yet.

Create: requirement traceability matrix; ADRs for modular monolith, tenancy, authentication, costing and posted-transaction corrections; prioritized backlog; risk register; repository conventions; initial C4-style architecture in Markdown/Mermaid; and an explicit MVP/non-MVP boundary. Cross-check workbook features against screenshot workflows and record gaps. Update `PROJECT_STATE.md`. Validate documentation links. Stop.

## Prompt 01 — Monorepo and Local Platform

Scaffold the React/Vite/TypeScript frontend, .NET solution layers, tests, PostgreSQL Docker Compose service, environment templates, formatting/linting, OpenAPI, health endpoints and baseline GitHub Actions CI. Pin versions and commit lockfiles. Add non-root multi-stage Dockerfiles. Implement no business modules beyond a health page/API. Run all builds/tests and Compose health checks. Update state and stop.

## Prompt 02 — Identity, Tenancy, Branches and Permissions

Implement secure authentication, tenants, memberships, branches, roles, permissions, plan/user limits, session revocation and audit events. Tenant context must come from authenticated membership. Add central query filters and database constraints. Include cross-tenant negative integration tests for every endpoint. Seed only safe development data. Add owner/admin/team/branch screens. Do not start inventory. Verify OWASP-relevant controls, migrations and E2E login. Update state and stop.

## Prompt 03 — Gas365 Design System and Application Shell

Implement the tokens and patterns in `reference/UI_SYSTEM.md` with Tailwind/shadcn. Build responsive navigation, header, branch selector, command/search placeholder, permission-aware routes, page shells, table/form primitives, loading/error/empty/denied states, toast/confirmation patterns and Storybook or a component showcase. Meet WCAG AA and add component/accessibility tests. Do not implement business logic. Update state and stop.

## Prompt 04 — Catalog, LPG, Cylinders, Spare Parts and Inventory Ledger

Implement configurable units, LPG products/weights, cylinder definitions and optional serial assets, spare-part categories/SKUs, warehouses/locations, stock buckets, movements, opening balances, transfers and adjustments. Stock is ledger-derived. Use decimal precision, idempotent posting, optimistic concurrency and reversal. Implement weighted-average costing and immutable cost snapshots with tests. Add stock directory/detail/ledger UI and import-ready DTOs. Prove stock reconciliation and tenant isolation. Update state and stop.

## Prompt 05 — Suppliers and Purchasing

Implement suppliers, purchase drafts, multi-line items, landed-cost allocation, posting, supplier payable/cash payment, partial payments, details and PDF voucher. Posting must be atomic and idempotent. Posted purchases use reversal/correction, not destructive edit. Add purchase list/create/detail and supplier profile/ledger UI. Add calculation, concurrency, permission and E2E tests. Update state and stop.

## Prompt 06 — Customers, Sales, Exchanges and Receipts

Implement customers, price lists, multi-line sales, gas-only/refill, full-cylinder, cylinder-only, empty-for-filled exchange and spare-part sales. The backend calculates prices, discounts, net, paid, balance, stock, COGS and gross profit. Posting must be atomic/idempotent and prevent invalid stock/cylinder states. Add receipt PDF, sales register/details, customer profile/history and payment capture. Add reversal flows and comprehensive tests. Update state and stop.

## Prompt 07 — Credit, Expenses, Loans and Cash Register

Implement append-only customer/supplier ledgers with allocations and unapplied amounts, aging, expenses, owner funds, loans/repayments, bank/cash accounts and branch cash-register open/close/reopen controls. Enforce the reconciliation formula in `BUSINESS_RULES.md`. Closed days require permissioned adjustment/reopening. Build Udhaar, expense, loan and cash-register screens and tests. Update state and stop.

## Prompt 08 — Dashboard, Reports, PDF and Excel/CSV

Implement server-side report queries and definitions for every report listed in `DEVOPS_TESTING_REPORTING_AI.md`. Reconcile report totals to ledgers with automated tests. Build responsive dashboard cards and Recharts visualizations using server aggregates. Add branch/date/type filters and permission-aware PDF/CSV/XLSX exports. Large exports use background jobs and private expiring downloads. Audit exports. Update state and stop.

## Prompt 09 — Audit, Imports, Notifications and Corrections

Implement complete audit timeline, reversal explorer, Excel/CSV import with mapping/preview/validation/error download/idempotent commit, low-stock and overdue-credit notifications, daily closing reminders and reliable job processing. Never import directly into posted ledgers without a validated command. Add operator screens and failure/retry tests. Update state and stop.

## Prompt 10 — Subscriptions, Onboarding and Platform Administration

Implement plan catalogue, tenant subscription state, server-enforced limits, onboarding checklist, usage metering and platform administration. Platform support access must be least-privilege, time-bounded where possible and audited. Do not expose tenant financial data by default. Use a payment-provider abstraction; do not invent live credentials. Add tests and update state.

## Prompt 11 — Production Hardening and Staging

Complete security headers, rate limiting, secret handling, backup/restore runbooks, database retention, observability, alerting, performance indexes, load tests, accessibility audit, dependency/container scans and staging deployment. Test restore into an isolated database. Test migrations from the last release. Add production approval and rollback workflow. Produce launch-readiness evidence and stop; do not deploy production without explicit authorization.

## Prompt 12 — Pilot and Evidence-Based Automation/AI

Create controlled pilot onboarding, feedback/incident templates, product analytics with privacy controls, SLOs and support playbooks. Establish baseline data quality and only then implement one low-risk advisory feature: deterministic reorder suggestions before ML. Define success metrics and an opt-out. If evidence later supports ML, write an ADR for a versioned Python analytics service; do not add it merely because Python is available. Update state and stop.
