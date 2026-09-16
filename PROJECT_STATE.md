# Gas365 project state

## Current phase

Phase 01 — Monorepo and Local Platform: implemented and locally verified on 2026-09-16. Phase 02 has not started. GitHub Actions repeats acceptance checks on each push; remote results are attached to the commit.

## Completed acceptance criteria

- Read the master prompt, repository rules, phase prompts and all supplied reference Markdown.
- Inspected the 47-sheet workbook structure and all 142 archived screenshots at overview resolution.
- Created the source inventory, requirement matrix, five ADRs, prioritized backlog, risk register, conventions, C4-style diagrams and explicit scope boundary.
- Recorded source contradictions and implementation gates.
- Validated maintained Markdown links, Git whitespace and staged content exclusions.
- Reviewed the Phase 00 checkpoint before starting Phase 01.
- Built the React health page, four .NET layers, PostgreSQL service, non-root Docker images and one-command local bootstrap.
- Added pinned versions, lockfiles, formatting/linting, OpenAPI, health checks, tests and baseline CI.
- Verified the empty migration baseline and explicit migration command against real PostgreSQL.

## Decisions

React/TypeScript SPA/PWA; ASP.NET Core modular monolith; PostgreSQL. Initial hosting uses an application service and separate database; provider choice is open. Same-origin cookie authentication is the planned default. Weighted-average costing applies to new transactions with immutable COGS. Posted records use reversal and replacement. Source business files remain ignored. Neutral project wording and configured contributor identity apply to commits.

## Open decisions

See [risk register](docs/RISKS.md): legal entity/name/domain; tax/invoice/FBR applicability; hosting/session details; costing bucket, rounding, backdating, returns and cutover; cylinder ownership/deposits/serial policy; payment advances and closed-period corrections; delivery scope; subscription provider; recovery objectives and production infrastructure.

## Verification and limitations

See [Phase 01 report](docs/PHASE_01_REPORT.md). Frontend lint, types, build and 4 component tests passed. Backend Release build passed with zero warnings; 2 unit, 4 architecture and 6 PostgreSQL/API tests passed. Four desktop/mobile browser tests passed. OpenAPI, formatting and dependency audits passed. All three Compose services are healthy. Migration application, repeatability and model-drift checks passed.

Only migration history is created; no business tables or tenant-owned endpoints exist. Authentication and tenant-isolation runtime tests begin in Phase 02. The platform is local development infrastructure, not a production deployment. Private source inputs remain ignored.

Toolchain: .NET SDK 10.0.401, runtime/EF 10.0.12, Node 24.13.1, pnpm 12.4.2 and PostgreSQL 18.4. Local database port is 55432 because 5432 was unavailable on this machine. Bootstrap creates a random password in ignored `.env`; API startup never applies migrations automatically.

## Exact next prompt

Run **Prompt 02 — Identity, Tenancy, Branches and Permissions** from [prompts/PHASE_PROMPTS.md](prompts/PHASE_PROMPTS.md). Record its hosting/session decisions and execute only that phase when requested. Do not start it automatically.
