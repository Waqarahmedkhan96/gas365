# Gas365 Repository Rules

## Mission

Build Gas365 as a secure, auditable, multi-tenant LPG ERP for Pakistan. The source workbook defines important operational workflows; screenshots are inspiration, not a licence to copy protected branding, code, copy or assets.

## Execution discipline

- Work on one numbered phase at a time.
- Before editing, read `PROJECT_STATE.md` and all files directly referenced by the active phase.
- Inspect the current repository; never assume the previous phase completed correctly.
- Maintain a short plan with explicit acceptance criteria.
- Do not start the next phase automatically.
- Finish each phase with tests, documentation, migration notes and a checkpoint summary.
- Preserve unrelated user changes.
- Never weaken a test merely to make CI green.
- Never invent business rules silently. Record unresolved choices in `PROJECT_STATE.md` under `Open Decisions`.

## Architecture

- Modular monolith, not microservices.
- Backend owns all money, stock, permission and tenant decisions.
- Frontend never calculates authoritative totals.
- PostgreSQL is the source of truth.
- Every tenant-owned row includes `TenantId`.
- Every branch-owned transaction includes `BranchId`.
- Use `decimal`, never floating point, for money, kilograms, quantities and unit costs.
- Posted financial/stock transactions are not hard-deleted or silently edited; correct them with reversal and replacement entries.
- Use UTC timestamps plus the tenant's IANA time zone for presentation and business dates.
- API commands that create financial effects must support idempotency.
- Use optimistic concurrency where simultaneous edits matter.

## Security

- Authentication is server-controlled; use secure HttpOnly cookies or a fully specified short-lived access/rotating-refresh token design. Do not store long-lived bearer tokens in localStorage.
- Authorization is permission-based and rechecked by the API.
- Never trust TenantId, BranchId, price, total, profit, COGS or permissions supplied by the browser.
- Apply tenant filters centrally and add database constraints/indexes that include TenantId.
- Add cross-tenant negative tests for every new tenant-owned module.
- Keep secrets outside source control.
- Validate uploaded file type, size and content; use private object storage.
- Log administrative access and all material mutations without logging passwords, tokens or sensitive contents.

## Quality gates

- Backend unit tests and PostgreSQL integration tests pass.
- Frontend lint, type-check and tests pass.
- Critical journeys have Playwright coverage.
- Migrations apply to an empty database and an upgraded prior database.
- OpenAPI remains valid.
- Docker Compose health checks pass.
- Documentation and `PROJECT_STATE.md` are updated.

## UI rules

- Use Gas365 naming and original icons/copy.
- Use the supplied GasFlow screenshots only for information architecture and interaction inspiration.
- Default palette for now: navy `#12385A`, dark navy `#0D2A45`, blue `#1F6FA8`, page `#F7F9FC`, surface `#FFFFFF`, border `#E4EAF0`, text `#1C2B3A`, success `#17A673`, warning `#F3A712`, danger `#D64545`.
- Meet WCAG AA contrast; status must never rely on colour alone.
- Build responsive desktop and mobile layouts.
- Use PKR and tenant-configurable locale/time zone.

## Forbidden shortcuts

- No hard-coded demo credentials in production.
- No direct entity exposure from controllers.
- No generic repository wrapper over EF Core unless it solves a demonstrated need.
- No business logic in React components or API controllers.
- No automatic production deployment without an environment approval gate.
- No Python service for normal charts. Use Recharts; reserve Python for later analytics/ML only.
