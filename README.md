# Gas365

Gas365 is a planned multi-tenant LPG operations platform for retailers and distributors in Pakistan. It covers LPG, cylinders, spare parts, purchasing, sales, credit, cash and profitability.

The repository includes the local platform, identity/workspace foundation and Phase 03 design system: verified email sign-in, tenants, branches, team permissions, session management, audit activity and a responsive, accessible application shell. Phase 04 adds catalog and inventory implementation; its checkpoint and outstanding costing decision are recorded below. Purchasing, sales and payments remain later phases.

## Run locally

With Docker's Linux engine and Node installed, run `node scripts/bootstrap.mjs`, then open [localhost:8080](http://localhost:8080). The script creates local credentials and waits for healthy services. See [local development](docs/LOCAL_DEVELOPMENT.md) for exact tools, hot reload, tests and migrations.

## Project documentation

- [Project state and next step](PROJECT_STATE.md)
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
- [Inventory rules and API contracts](docs/INVENTORY.md)
- [Design system and application shell](docs/DESIGN_SYSTEM.md)
- [Controlled phase prompts](prompts/PHASE_PROMPTS.md)
- [Master prompt](00_MASTER_PROMPT.md)

## Stack

React, TypeScript and Vite; ASP.NET Core Web API and EF Core; PostgreSQL; Docker Compose; GitHub Actions. Versions are pinned in runtime files, package manifests, lockfiles and Dockerfiles.

Original workbooks, screenshots and the supplied development pack remain local and excluded from Git. Repository documentation contains workflow descriptions, not customer records or copied third-party assets.
