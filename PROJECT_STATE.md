# Gas365 project state

## Current phase

Phase 02 — Identity, Tenancy, Branches and Permissions: implemented and locally verified on 2026-09-16. GitHub Actions repeats acceptance checks on each push; remote results are attached to the commit. Phase 03 has not started.

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
- Implemented verified-email authentication, tenant membership, branch access, role permissions, quota enforcement, session revocation and audit events.
- Verified cross-tenant endpoint isolation, composite constraints, guarded writes, live permission changes, lockout, CSRF, cookie flags and reset replay protection.
- Added responsive workspace administration and desktop/mobile email-verification/login/branch/logout journeys.
- Verified fresh and Phase 01 upgrade migrations, persistent local identity keys and local email delivery.

## Decisions

React/TypeScript SPA/PWA; ASP.NET Core modular monolith; PostgreSQL. Initial hosting uses an application service and separate database; provider choice is open. Same-origin cookie authentication is implemented with PostgreSQL session revocation, eight-hour absolute and 30-minute idle expiry. Email links expire after one hour. Weighted-average costing applies to new transactions with immutable COGS. Posted records use reversal and replacement. Source business files remain ignored. Neutral project wording and configured contributor identity apply to commits.

## Open decisions

See [risk register](docs/RISKS.md): legal entity/name/domain; tax/invoice/FBR applicability; production hosting, protected keys, SMTP and optional MFA/phone recovery; costing bucket, rounding, backdating, returns and cutover; cylinder ownership/deposits/serial policy; payment advances and closed-period corrections; delivery scope; subscription provider; recovery objectives and production infrastructure. Provisioning defaults of 3 active branches, 10 users and 3 administrators are local development limits pending commercial plan decisions. Keep at least one active branch; owner transfer/removal is not exposed in this phase.

## Verification and limitations

See [Phase 02 report](docs/PHASE_02_REPORT.md). Frontend lint, types, build and 6 component tests passed. Backend Release build, 2 unit, 4 architecture and 26 PostgreSQL/API tests passed. Six desktop/mobile browser tests passed. OpenAPI and dependency audits passed. Compose starts PostgreSQL, Mailpit, API and web after a successful migration job. Migration application, upgrade, repeatability and model-drift checks passed.

Identity/workspace tables and tenant-owned administrative endpoints now exist. Inventory and financial modules have not started. The platform remains local development infrastructure, not a production deployment. Private source inputs remain ignored. Production email, encrypted shared keys, coordinated rate limits and optional MFA remain explicit gates.

Toolchain: .NET SDK 10.0.401, runtime/EF 10.0.12, Node 24.13.1, pnpm 12.4.2 and PostgreSQL 18.4. Local database port is 55432 because 5432 was unavailable on this machine. Bootstrap creates a random password in ignored `.env`; API startup never applies migrations automatically.

## Exact next prompt

Run **Prompt 03 — Gas365 Design System and Application Shell** from [prompts/PHASE_PROMPTS.md](prompts/PHASE_PROMPTS.md), only when requested. Do not start it automatically.
