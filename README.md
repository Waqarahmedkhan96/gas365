# Gas365

Gas365 is a planned multi-tenant LPG operations platform for retailers and distributors in Pakistan. It covers LPG, cylinders, spare parts, purchasing, sales, credit, cash and profitability.

Gas365 covers the full LPG business cycle: verified sign-in with tenant and branch permissions; inventory with weighted-average costing; purchasing; sales, exchanges and cylinder custody with deposits; credit (udhaar), expenses, loans and cash registers; reconciled reports with CSV, Excel and PDF exports and a dashboard; validated Excel/CSV imports and opening balances; alerts and an audit and corrections explorer; subscriptions with owner-approved support access; and a hardened, portable deployment. The functional MVP (Phases 00–08) and Phases 09–11 are implemented and locally verified. See [project state](PROJECT_STATE.md) for the current checkpoint.

## Run locally

With Docker's Linux engine and Node installed, run `node scripts/bootstrap.mjs`, then open [localhost:8080](http://localhost:8080). The script creates local credentials and waits for healthy services. See [local development](docs/LOCAL_DEVELOPMENT.md) for exact tools, hot reload, tests and migrations.

## Project documentation

- [Project state and next step](PROJECT_STATE.md)
- [Combined phase history and new-chat handoff](docs/PHASE_HISTORY.md)
- [Source inventory and workflow gaps](docs/SOURCE_INVENTORY.md)
- [Requirement traceability](docs/REQUIREMENTS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Architecture decisions](docs/adr/README.md)
- [Prioritized backlog](docs/BACKLOG.md)
- [MVP boundary](docs/SCOPE.md)
- [Risks and open decisions](docs/RISKS.md)
- [Repository conventions](docs/CONVENTIONS.md)
- [Phase 00 verification](docs/PHASE_00_REPORT.md)
- [Phase 01 verification](docs/PHASE_01_REPORT.md)
- [Phase 02 verification](docs/PHASE_02_REPORT.md)
- [Phase 03 verification](docs/PHASE_03_REPORT.md)
- [Phase 04 checkpoint](docs/PHASE_04_REPORT.md)
- [Phase 05 checkpoint](docs/PHASE_05_REPORT.md)
- [Purchasing and supplier contracts](docs/PURCHASING.md)
- [Inventory rules and API contracts](docs/INVENTORY.md)
- [Design system and application shell](docs/DESIGN_SYSTEM.md)
- [Sales, cylinders and receipts](docs/SALES.md)
- [Credit, expenses, loans and cash registers](docs/FINANCE.md)
- [Reports, dashboard and exports](docs/REPORTS.md)
- [Imports, opening balances, notifications and audit](docs/OPERATIONS.md)
- [Subscriptions, onboarding and platform administration](docs/PLATFORM.md)
- [Deployment runbook](docs/DEPLOYMENT.md)
- [Launch readiness](docs/LAUNCH_READINESS.md)
- Phase reports: [06](docs/PHASE_06_REPORT.md), [07](docs/PHASE_07_REPORT.md), [08](docs/PHASE_08_REPORT.md), [09](docs/PHASE_09_REPORT.md), [10](docs/PHASE_10_REPORT.md), [11](docs/PHASE_11_REPORT.md)
- [Controlled phase prompts](prompts/PHASE_PROMPTS.md)
- [Master prompt](00_MASTER_PROMPT.md)

## Stack

React, TypeScript and Vite; ASP.NET Core Web API and EF Core; PostgreSQL; Docker Compose; GitHub Actions. Versions are pinned in runtime files, package manifests, lockfiles and Dockerfiles.

Original workbooks, screenshots and the supplied development pack remain local and excluded from Git. Repository documentation contains workflow descriptions, not customer records or copied third-party assets.
