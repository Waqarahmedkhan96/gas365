# Phase 09 checkpoint

Status: implementation and required local checks complete on 2026-09-29 on `feature/phase-09-operations`. Git integration is recorded in [project state](../PROJECT_STATE.md).

## Scope and acceptance

Prompt 09: a complete audit timeline and a reversal explorer; Excel/CSV import with mapping, preview, validation, error download and idempotent commit; low-stock and overdue-credit notifications and daily closing reminders; reliable job processing. No direct import into posted ledgers without a validated command; operator screens and failure and retry tests.

All criteria are met. The operating contract is in [imports, opening balances, notifications and audit](OPERATIONS.md).

## Owner decisions (2026-09-29)

- Imports cover master data plus openings: customers, suppliers, catalog items, opening stock and opening customer and supplier balances.
- Credit is overdue more than 30 days after the invoice date, adjustable per business (1–365 days).
- Notifications are in-app; email and SMS wait for production mail setup (Phase 11).
- Open drawers raise a closing reminder at 21:00 tenant time, adjustable per business.

## Delivered

- **Opening balances:** a document with an append-only journal, created with a command key and finance.manage. Aging uses the original invoice date. They are settled by allocating receipts and payments, and reversed only after their allocations. They are included in the Udhaar view, aging reports, supplier ledger, dashboard and overdue alerts.
- **Imports:** content-validated XLSX and CSV parsing, including RFC 4180 quoting and Excel double precision. Automatic column mapping with aliases; row validation mirroring every command rule; a problems-first preview; a formula-safe error CSV. The commit is claimed atomically and revalidated, then runs row by row through existing commands. Ledger rows use deterministic idempotency keys, so a partly failed import resumes without duplicates.
- **Notifications:**
  - an idempotent sweeper with a per-tenant advisory lock and a partial unique index;
  - low-stock, overdue-credit and open-register alerts that resolve automatically;
  - visibility by the viewer's permissions and branch access, and per-person read state;
  - a header bell with unread count, a notifications page, and settings fields.
- **Audit:** a filterable timeline (area, actor, record) and a corrections explorer across six ledgers with reason, author and amount.
- **Jobs:** `JobStates` records health for the export and notification workers. Export retry and final failure are proven.
- **Other:** the `imports.manage` permission, and sidebar copy that no longer lists shipped modules as upcoming.

## Corrections during verification

1. The import preview did not mirror two command rules, so rows would have passed the preview and then failed at commit. It now checks the catalog rule (LPG and cylinders need a capacity and a classification) and the one-opening-per-stock-state rule.
2. The overdue query used grouping shapes EF cannot translate. It now uses projected aggregate subqueries that PostgreSQL executes as grouped joins, so only open balances are returned.
3. The Phase 02 upgrade test seeded tenants with the current model; it now uses raw SQL at that schema version.
4. The new Udhaar correction column made its table scroll on mobile. The finance tables are now focusable labelled regions, fixing an axe finding.
5. The notification list repeated the alert kind before a title that already contained it.
6. The obsolete 100-event audit fetch was removed from the workspace load; the audit page queries on demand.
7. The design-system test's list of pages visible without permissions now includes Notifications, which is intentionally visible to everyone with per-alert permission filtering. No assertion was removed.

## Verification run in this chat — 2026-09-29

| Check                                                 | Result                                                                                                                                                                       |
| ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build / `dotnet format`               | 0 warnings, 0 errors / passed                                                                                                                                                |
| Architecture / unit / PostgreSQL integration          | 4 / 63 / 82 passed (149). New: opening balances, the import lifecycle and resume, notifications, audit and corrections, export retry and job health; extended migration test |
| Frontend format, lint, types, build / component tests | Passed / 20 passed                                                                                                                                                           |
| Browser tests (desktop + mobile)                      | 26/26 passed, including the new import, opening-balance, alert and audit journey with axe                                                                                    |
| Migrations                                            | Fresh, upgrade (with Phase 07 seed data), repeat and model drift; applied to the local database by the Compose migrate job                                                   |
| OpenAPI / Compose / audits                            | Passed / healthy / no known vulnerabilities                                                                                                                                  |

Earlier browser runs in this chat failed before the corrections above (3/26, then 2/26 twice), and the final run supersedes them. One targeted test command was slow once (over 10 minutes of wall time); it did not recur across three later runs, including the full suite under a hard timeout. No remote CI or production deployment is claimed.

## Migration impact

Apply `20260929092809_AuditImportsAndNotifications` before starting the updated API. It adds tenant notification settings (existing tenants default to 30 days and 21:00), the opening-balance tables with append-only triggers, a finance-entry reference, and the notification, import and job-health tables. It grants imports.manage to owners. No posted history changes. The local Compose file sets `Notifications__SweepSeconds: 15`; the production default is 60.

## Limitations

- Email and SMS notification channels and scheduled report delivery are not built.
- Import files are parsed into database rows; the raw file is not kept.
- Imports cannot create serial assets; register them before importing serial-tracked opening stock.
- Job health is recorded but not yet exposed on a platform administration screen (Phases 10–11).
