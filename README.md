# Gas365

Gas365 is a planned multi-tenant LPG operations platform for retailers and distributors in Pakistan. It covers LPG, cylinders, spare parts, purchasing, sales, credit, cash and profitability.

The repository includes verified email sign-in, tenant/branch permissions, audit activity and a responsive application shell. Inventory uses the costing policy accepted on 2026-09-19. Purchasing provides suppliers, drafts, partial cash allocations and PDF vouchers. Phase 06 adds customers, price lists, sales, filling loss, exchanges, cylinder custody, deposits, refunds and receipts, and separates purchase-related cost payments from the supplier balance. See [sales contracts](docs/SALES.md) and the [Phase 06 report](docs/PHASE_06_REPORT.md) for verification status and operating boundaries.

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
- [Controlled phase prompts](prompts/PHASE_PROMPTS.md)
- [Master prompt](00_MASTER_PROMPT.md)

## Stack

React, TypeScript and Vite; ASP.NET Core Web API and EF Core; PostgreSQL; Docker Compose; GitHub Actions. Versions are pinned in runtime files, package manifests, lockfiles and Dockerfiles.

Original workbooks, screenshots and the supplied development pack remain local and excluded from Git. Repository documentation contains workflow descriptions, not customer records or copied third-party assets.
