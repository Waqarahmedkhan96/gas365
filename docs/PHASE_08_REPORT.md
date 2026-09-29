# Phase 08 checkpoint

Status: implementation and required local checks complete on 2026-09-29 on `feature/phase-08-reports`. Git integration is recorded in [project state](../PROJECT_STATE.md). The owner directed on 2026-09-29 that the remaining phases continue in the same chat, one feature branch per phase.

## Scope and acceptance

Prompt 08: server-side queries and definitions for every report in `reference/DEVOPS_TESTING_REPORTING_AI.md`; totals reconciled to ledgers by automated tests; a responsive dashboard with Recharts built on server aggregates; branch, date and type filters; permission-aware PDF, CSV and XLSX exports; background jobs with private, expiring downloads; audited exports. Also carried from Phase 07: a per-category cash reconciliation.

All criteria are met. Report semantics, filter contracts and the export lifecycle are in [reports](REPORTS.md).

## Delivered

- **Fifteen reports**, each with a definition, a reconciliation rule and required permissions: daily sales, purchases, stock on hand, stock movement, inventory valuation, LPG profit, spare-parts profit, expenses, gross and net profit, customer aging, supplier payables, cash reconciliation, cylinder custody, branch comparison and user activity.
- **Exact category profit:** spare-part revenue comes from the frozen sale snapshot and spare-part cost from the sale's stock movements. LPG is the journal amount minus spare parts, so categories always sum to the sale journal.
- **Cash reconciliation by formula line.** This closes the Phase 07 carry-over.
- **Dashboard:** permission-aware metrics, a daily trend line, and expense-category and branch revenue bars. The charts use the validated categorical palette, with legends in ink, a dashed secondary encoding, a table view for every chart and no animation.
- **Reports page:** filters, the definition and reconciliation text shown with the data, paging with totals across every row, exports and a private export list.
- **PostgreSQL-backed export queue** processed by a hosted worker:
  - jobs are claimed with `SKIP LOCKED` under a five-minute lease and retried up to three attempts;
  - the requester's live permissions are rebuilt before running;
  - files are formula-injection-safe CSV, XLSX with numeric cells and an About sheet, or landscape paginated PDF;
  - downloads are available only to the requester, expire after 24 hours, and requesting, completing and downloading are all audited.
- **Permissions:** `reports.view` and `reports.export`, granted to owner roles by migration.

## Decisions

These are technical choices; no business rule was invented. Report semantics follow the approved ledgers and policies from earlier phases.

- Reports include inactive branches so history stays visible.
- Point-in-time reports use the To date as the as-of date.
- User activity groups audit prefixes into six business areas.
- Export files live in PostgreSQL for the local deployment; private object storage is a Phase 11 production item.
- Limits: 366-day periods, 100 rows per page, 100,000 rows per report, 5,000 rows per PDF, and five in-flight exports per member.
- Open XML SDK 3.5.1 (MIT, Microsoft) generates XLSX files. Recharts 3.10.1 is pinned exactly. Both passed the dependency audits.
- The PDF font resolver now simulates bold and italic from the single embedded face; existing vouchers do not request bold, so they are unchanged.

## Corrections during verification

1. Npgsql accepts only UTC timestamps, so user-activity day boundaries are now converted from tenant-local midnight to UTC.
2. `from` is a LINQ query keyword, so two query expressions escape it as `@from`.
3. Axe flagged the horizontally scrolling mobile report table as unreachable by keyboard. Scroll containers are now focusable, labelled regions.
4. Visual review of screenshots found:
   - squashed mobile table cells, now using the existing `.data-table` styles with no-wrap cells;
   - series-colored legend text, now in ink;
   - a clipped top data point, crowded dots and an animated first render;
   - a smoothed curve implying values between days, now straight segments.
5. Downloads fetch the file with the required `X-Workspace` header and save it from a blob URL; a plain link would have been rejected with 409.

## Verification run in this chat — 2026-09-29

| Check                                                | Result                                                                                                                                                    |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build / `dotnet format`              | 0 warnings, 0 errors / passed                                                                                                                             |
| Architecture / unit / PostgreSQL integration         | 4 / 63 / 77 passed (144). New: 23 report unit cases; reconciliation, permission/isolation and export-lifecycle integration tests; extended migration test |
| Frontend format, lint, types, build                  | Passed; the dashboard is lazy-loaded (Recharts stays out of the main bundle)                                                                              |
| Frontend component tests                             | 17 passed                                                                                                                                                 |
| Browser tests (desktop + mobile, rebuilt containers) | 24/24 passed, including the new dashboard, report and download journey with axe checks                                                                    |
| Migrations                                           | Fresh, Phase 07 upgrade, repeat and model-drift checks in `PlatformTests` (13 migrations); applied to the local database                                  |
| OpenAPI / Compose / audits                           | Passed / healthy / no known vulnerabilities                                                                                                               |

An earlier full browser run failed 1 of 24 cases (mobile axe finding) before correction 3. The final run supersedes it. No remote CI run or production deployment is claimed.

## Migration impact

Apply `20260929080123_ReportingAndExports` before starting the updated API. It is additive: the export-job table, business-date indexes on five journals, and reporting permissions for owners. No posted history changes. Downgrade drops the export table and removes the permissions.

## Limitations

- Report queries aggregate in the application after indexed range scans. That is appropriate at pilot scale; load testing and query tuning belong to Phase 11.
- Export files are stored in PostgreSQL rather than private object storage.
- No scheduled report delivery yet (Phase 09 automation).
- Locale formatting uses en-PK; tenant-configurable locale remains an open decision.
