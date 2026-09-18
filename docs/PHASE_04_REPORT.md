# Phase 04 checkpoint

Status on 2026-09-18: implementation complete and locally verified; final phase acceptance remains pending the costing-policy choice. Phase 05 has not started.

## Outcome

Catalog units and categories, LPG/cylinder/spare-part definitions, branch warehouses, optional serial assets, ledger-derived stock, opening balances, adjustments, transfers and reversal are implemented. Stock directory/detail/ledger screens use the shared shell and active branch context. API commands use decimal values, idempotency keys and optimistic stock/catalog versions.

## Files and architecture

- Domain: inventory entities, permission constants and replaceable weighted-average costing implementation.
- Application: typed catalog, asset, balance, ledger and command DTOs plus service contracts.
- Infrastructure: catalog/stock services, central tenant filters, composite constraints and two migrations.
- API: authenticated inventory routes under the existing workspace/CSRF boundary; no entity exposure or controller business rules.
- Frontend: lazy-loaded Inventory page, branch context, role permissions and shared forms/tables/dialogs.
- Tests: costing units, PostgreSQL/API invariants, upgrade preservation and real desktop/mobile inventory journeys.

See [inventory rules and contracts](INVENTORY.md) for the exact behaviors, operating bounds and import-ready payload.

## Decisions and limits

Owner-selected rules: current-date posting only; full reversals only without later affected movements; optional item-level cylinder serial tracking; deposits deferred to sales. The proposed product/warehouse cost pool, numeric scales and rounding are awaiting confirmation. Opening quantity/value must be independently reconciled by the operator; no source history or business data was imported.

No purchasing, sales, payment/deposit accounting, bulk import, arbitrary backdating, negative stock or partial reversal was introduced. Tenant-wide stock locks favor correctness over maximum within-tenant write throughput. Production gates from prior phases remain applicable.

## Verification record

- Backend Release build passed: **4 unit, 4 architecture and 36 PostgreSQL/API tests**.
- Ten inventory integration cases verify receipt/issue values, cross-branch transfers, reversal identity and dependencies, concurrent issues and retries, serial custody, every route's workspace boundary, forbidden branches, catalog versions, composite constraints and database immutability.
- Frontend lint, type-check, production build and **13 component tests** passed, including CSRF token renewal without automatic command replay.
- **16 browser tests passed** across desktop and mobile, including existing identity/shell journeys, inventory accessibility, focus return, opening/transfer/reversal and retry after a lost committed response.
- Formatting, OpenAPI, no-pending-model-change checks, fresh/upgrade/repeat migrations, Compose health, Markdown links, Git whitespace and both dependency audits passed. Total: **73 tests**. GitHub Actions repeats these checks for the pushed checkpoint.

An initial integration run detected missing database immutability guards; the guard migration now passes the raw SQL mutation tests. Browser locator errors were corrected without relaxing the behavior assertions. A Windows port reservation required changing only the ignored local database port to 54320. The complete browser suite exposed unnecessary per-command CSRF fetches consuming the authentication rate budget; the client now reuses the request token in memory and invalidates it after authentication transitions or rejected requests. Server CSRF checks and authentication limits are unchanged.

## Security, migration and next step

Tenant filters, tenant-qualified foreign keys, branch authorization and permission checks cover every new module. Posting atomically persists documents, movements and audit events. Posted records reject updates/deletes at both EF and database levels. Negative stock, stale versions, duplicate serial custody and conflicting command-key reuse are rejected. Existing owners receive inventory permissions on upgrade; team access requires explicit role assignment.

Apply `InventoryLedger` and `InventoryGuards` before starting the updated API. No inventory is seeded. Fresh and prior-schema upgrades are tested; downgrade is destructive to inventory data and is not a recovery plan.

Resolve the remaining costing decision before accepting Phase 04. The subsequent prompt is **Prompt 05 — Suppliers and Purchasing**, only when requested.
