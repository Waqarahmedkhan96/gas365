# Phase 07 checkpoint

Status: implementation and required local checks complete on 2026-09-28 on `feature/phase-07-finance`. Git integration is recorded in [project state](../PROJECT_STATE.md). Phase 06 remains complete at implementation `3a7d055`, merge `d222c75`. Its historical checks were not rerun as Phase 07 evidence; the current full suites below include the Phase 06 tests.

## Scope and acceptance

Implement append-only credit and unapplied allocations, aging, expenses, owner funds, loans and repayments, branch cash/bank accounts and register open/close/reopen. Preserve prior posted history. Verify tenant/branch isolation, permissions, exact decimal reconciliation, idempotency, concurrency, allocation/reversal dependencies, closed-day enforcement across earlier payment paths, migrations, backend/frontend/browser checks, OpenAPI and Compose health. Integrate only after completion and required checks.

All criteria are met by the implementation and checks below. Operating contracts are in [finance contracts](FINANCE.md).

## Delivered

- Four tenant-owned tables: money accounts, expense categories, finance entries and register events, with composite tenant/branch keys, idempotency indexes, check constraints and append-only triggers.
- Customer and supplier advances with invoice allocations, owner funds, expenses, principal-only loans and repayments, account transfers, reasoned cash adjustments and dependency-ordered reversals.
- Register open/close/reopen with counted, expected and variance history and optimistic concurrency. Once a branch has a default cash drawer, earlier sale, purchase and cylinder cash paths are captured atomically into it, and cash is rejected on unopened or closed days.
- Udhaar (credit and aging), expenses, loans and owner funds, and cash-register screens with permission-aware navigation.
- Six permissions granted to owner roles by migration.

## Decisions

On 2026-09-28 the owner approved invoice-date aging (Current/1–30/31–60/61–90/90+), principal-only loans, operator-reconciled account opening balances, and recorded cash variances requiring a separate reasoned adjustment before book cash changes. No automatic interest or due-date terms are implied.

Security correction during verification: all `/api/auth/*` endpoints shared one 60-requests-per-minute per-IP budget, including the session checks the app makes on every page load. Users behind a shared shop or mobile-carrier IP could be blocked, and the finance browser test was rejected with 429. When asked, the owner delegated the choice to industry best practice. Login, registration, confirmation, reset, resend, switch, logout and session revocation keep the strict 60/min per-IP budget. `GET /csrf`, `/me` and `/sessions` now use a separate budget of 300/min, counted per signed-in user or per IP when anonymous. The existing burst test now probes login attempts, and a new test covers both budgets.

## Corrections made during verification (2026-09-28)

1. Closed or unopened register rejections from the database guard previously surfaced as a generic 409 conflict. The API now returns the guard's message with instructions to open or reopen the register. The existing integration test asserts the message.
2. The finance screen's selects are now labelled with `htmlFor` like the shared `Field` component, instead of wrapping the control. The browser test's exact match on the required "Amount (PKR)" field was changed to a regex, matching the rest of the spec. No assertions were removed.
3. Rate-limit split described above.
4. Whitespace-only line-ending fixes in files created earlier in the phase.

## Verification run in this chat — 2026-09-28

Toolchain: .NET SDK 10.0.401 installed user-locally at `%LOCALAPPDATA%\Gas365\dotnet`, Node 24.13.1, pnpm 12.4.2, PostgreSQL 18.4.

| Check                                                | Result                                                                                                                                                                                                   |
| ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build                                | 0 warnings, 0 errors                                                                                                                                                                                     |
| `dotnet format --verify-no-changes`                  | Passed after the whitespace fix                                                                                                                                                                          |
| Architecture / unit / PostgreSQL integration tests   | 4 / 40 / 72 passed (116). Includes 10 finance integration tests, 10 finance unit cases and the new rate-limit test                                                                                       |
| Frontend format, lint, types, build                  | Passed                                                                                                                                                                                                   |
| Frontend component tests                             | 14 passed                                                                                                                                                                                                |
| Browser tests (desktop + mobile, rebuilt containers) | 22/22 passed, no flaky or skipped cases                                                                                                                                                                  |
| Migrations                                           | Fresh, Phase 06 upgrade, repeat application and model drift verified by `PlatformTests` (11 migrations). The local database was already upgraded; the Compose migrate job reported no pending migrations |
| OpenAPI validation                                   | Passed                                                                                                                                                                                                   |
| Compose health                                       | web, api, postgres and mailpit healthy after rebuild                                                                                                                                                     |
| pnpm audit (high) / NuGet vulnerable-package audit   | No known vulnerabilities                                                                                                                                                                                 |

An earlier browser run in this chat failed 2 of 22 cases (the finance journey on desktop and mobile) before corrections 2 and 3. The final run above supersedes it. No remote CI run or production deployment is claimed.

## Migration impact

Apply `20260928120055_FinanceAndRegisters` before starting the updated API. It is additive: it creates tables, guards, capture triggers and owner permissions, creates no finance rows and changes no posted history. Team roles need explicit assignment of the new permissions. Creating a branch's first cash account activates drawer controls for that branch, so open the register before taking cash. Downgrade is destructive; use backup and restore.

## Limitations

- Advances cannot yet be allocated to cylinder settlement receivables, although those appear in aging.
- Expense categories and accounts cannot be renamed or retired.
- Expected close is a single signed total; a per-category breakdown of the reconciliation formula is left for Phase 08 reporting.
- Finance list endpoints compute unapplied balances per row; revisit with measurements in Phase 08 or 11.
- Tenant-wide posting lock and production gates are unchanged from earlier phases.

## Historical draft checkpoint (earlier on 2026-09-28)

The initial draft added the four tables, captured earlier cash paths into the default drawer, and recorded the owner's policy approvals. At that point only an initial Release build had passed, and the migration was generated but unverified. The verification above completes that draft.

## Next action

Phase 07 is complete. Do not start Phase 08 unless the owner requests it.

## Phase 07 Git integration completed — 2026-09-28

Verified implementation checkpoint: `bbd39ee`. Merge commit: `2eb81ba` on `main`, pushed successfully to origin. The merged local `feature/phase-07-finance` branch was deleted; no remote feature branch existed. The secondary detached worktree was preserved. Phase 07 is complete; Phase 08 has not started.
