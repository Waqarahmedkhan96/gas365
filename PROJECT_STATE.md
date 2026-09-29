# Gas365 project state

## Current phase

Phase 07 — Credit, Expenses, Loans and Cash Register: complete and locally verified on 2026-09-28; Git integration is recorded at the end of this file. See [Phase 07 report](docs/PHASE_07_REPORT.md) and [finance contracts](docs/FINANCE.md). Phase 08 has not started. Earlier Phase 06 and Phase 07 in-progress paragraphs below are historical.

## Previous phase checkpoint

Phase 05 — Suppliers and Purchasing: implemented and locally verified on 2026-09-21. Historical checkpoint: business acceptance was pending. The owner later retained separate payment reversal and rejected supplier-paid transport charges; see Open Decisions. See [Phase 05 report](docs/PHASE_05_REPORT.md). Phase 04 was accepted on 2026-09-19; its approved costing policy is unchanged. This records the prior Phase 05 checkpoint. Implementation checkpoint: `a3b75a0`, including backend commit `8e2d9f8`, on `feature/phase-05-purchasing`.

## Cross-chat handoff

Use [combined phase history](docs/PHASE_HISTORY.md) for the cumulative record of Phases 00–05 and the reusable new-chat prompt. Work on one numbered phase per chat according to [repository rules](AGENTS.md). Update this state, the combined history and the detailed phase report at every checkpoint.

On 2026-09-19, repository reports, Git checkpoints, source layout, costing implementation, migrations and CI configuration were reviewed to consolidate the history. This was a documentation-only handoff update; application suites and remote CI were not rerun. The owner subsequently confirmed the costing policy on 2026-09-19, closing Phase 04 acceptance; Phase 05 had not started at that historical checkpoint.

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

- Implemented Tailwind tokens, shared accessible components and a responsive application shell with permission-aware URLs.
- Added branch context, keyboard page search, dialogs, notifications and an owner component showcase.
- Verified keyboard/focus behavior, 320 px layouts, contrast tokens, axe accessibility checks and strict CSP dialog support.

## Decisions

React/TypeScript SPA/PWA; ASP.NET Core modular monolith; PostgreSQL. Initial hosting uses an application service and separate database; provider choice is open. Same-origin cookie authentication is implemented with PostgreSQL session revocation, eight-hour absolute and 30-minute idle expiry. Email links expire after one hour. Weighted-average costing applies to new transactions with immutable COGS. Posted records use reversal and replacement. Source business files remain ignored. Neutral project wording and configured contributor identity apply to commits.

## Phase 04 acceptance checkpoint

- Catalog, configurable units, LPG/cylinder definitions, spare-part categories and branch-restricted warehouses.
- Ledger-derived stock, atomic transfers, idempotency, optimistic concurrency and immutable cost snapshots.
- Optional serial assets and full reversal with dependency checks.
- Directory/detail/ledger UI, real PostgreSQL tenant/concurrency tests, migration upgrade, browser journeys and CI.

The owner selected current-date posting only, full reversals only without later affected stock movements, optional serial tracking per cylinder item, and deposits deferred to sales. On 2026-09-19 the owner approved the existing costing rules for current use: separate tenant/product/warehouse weighted-average pools across stock states, configured quantity precision up to 3 decimals (whole cylinders), cost precision of 6 decimals, PKR values of 2 decimals, midpoint rounding away from zero and full depletion consuming the remaining value. The owner may request changes in a later phase; any change must preserve posted history and document its effective scope and migration impact. Opening quantities/values must be reconciled by the operator; historical FIFO records are not imported or recosted.

## Approved cylinder deposit and usage policy

The owner accepted the recommendations in this task and clarified that workers enter the usage charge manually on return. It may differ each time; PKR 2,000 was an example, not a fixed or monthly rate. No automatic duration-based charge is authorized.

- Configure a suggested deposit per cylinder type, but permit manual non-negative deposit amounts, including zero, subject to permissions. Reductions/waivers require authorized staff; admin/store-manager-approved zero-deposit customer arrangements support hotels and tandoors.
- Borrowed cylinders remain business-owned and are tracked against the customer even when no deposit is collected. Return moves the cylinder to empty stock or inspection according to condition, without treating the shell as a new purchase.
- A worker with return/usage-entry permission can enter the variable usage fee when processing the return. Do not require manager entry or approval of an ordinary usage fee merely because damage/loss charges need approval. Record actor, amount and return reference; show fee terms to the customer when issuing the cylinder.
- Damage or loss charges require admin/store-manager approval; ordinary workers cannot approve them. Permission is checked by the API, not inferred from a client role label.
- Deposit collection increases cash and refundable deposit liability, not sales income. The earned usage fee is separate income; applying it against the deposit reduces the liability. Refundable deposits are shown in cash received, separate from revenue/profit.
- Return settlement displays original deposit, usage fee, approved damage/loss charges and refund due. Only actual payment records a cash refund; an unpaid refund remains owed. Approved charges beyond a deposit, including a zero-deposit loan, must be collected separately rather than producing a negative refund.
- All cash, fee, custody and refund effects need atomic/idempotent posting and auditable reversal. This approved policy is implemented in Phase 06.

## Open decisions

Phase 07 operating rules approved on 2026-09-28: invoice-date aging (Current/1–30/31–60/61–90/90+), principal-only loans, operator-reconciled opening balances and recorded cash variances requiring a separate reasoned adjustment before book cash changes. These are implemented and verified. Open for later phases: allocating advances to cylinder settlement receivables; renaming or retiring accounts and expense categories; a per-category reconciliation breakdown (Phase 08 reporting).

Owner decisions recorded during Phase 06: petrol, tolls, loading and unloading are paid separately from the goods supplier, including costs of the business's own vehicle. The previous supplier-paid landed-charge assumption is rejected and requires a purchasing correction. Separate reasoned payment reversal before purchase reversal is approved. Borrowed cylinders and refundable deposits are explicitly included in Phase 06, not deferred.

On 2026-09-21 the owner chose to capitalize separately paid purchase-related costs into stock. New purchasing code separates their cash payments from the goods supplier balance; historical posted purchases retain their original treatment. Cylinder deposit and usage policy is approved; workers enter a variable fee on return. Exchange means receiving an empty cylinder and giving a filled cylinder. Filling permits custom quantities such as 0.1, 1.5, 1.6, 3.5 and 30 kg, with manual LPG loss for each cylinder's filling, displayed in red with a text label. The unanswered tracking clarification is handled by optional source-cylinder and customer-cylinder references on each filling line, as stated during implementation. These are traceable references, not a physical source-cylinder gas balance. A dedicated source-cylinder gas subledger remains an open future decision. Existing posted purchasing history must remain unchanged.

Cash-only allocations, no advances, and English PDF vouchers are existing boundaries; bank accounts/cash closing and full Urdu voucher typography remain later work. See [purchasing contracts](docs/PURCHASING.md).

See [risk register](docs/RISKS.md): legal entity/name/domain; tax/invoice/FBR applicability; production hosting, protected keys, SMTP and optional MFA/phone recovery; future return valuation and real-data cutover; cylinder customer/deposit relationships; payment advances and closed-period corrections; delivery scope; subscription provider; recovery objectives and production infrastructure. Provisioning defaults of 3 active branches, 10 users and 3 administrators are local development limits pending commercial plan decisions. Keep at least one active branch; owner transfer/removal is not exposed in this phase. Tenant locale configuration beyond browser locale remains open; dates use the existing tenant time zone. Inventory uses branch context and separately authorizes both source and destination branches.

## Verification and limitations

Historical Phase 04 verification: see [Phase 04 report](docs/PHASE_04_REPORT.md) and [inventory contracts](docs/INVENTORY.md). Its 73-test result belongs to 2026-09-18 and is not the current Phase 05 result.

Current Phase 05 verification (2026-09-20–21): backend Release build/publish, 11 unit, 4 architecture, 44 PostgreSQL/API and 13 frontend tests passed. Frontend lint/types/build, fresh and Phase 04 upgrade migrations, repeat application, model drift, OpenAPI, dependency audits and Compose health passed. PDF fixtures with 2 and 100 lines were rendered and visually checked across all pages. All 18 desktop/mobile browser tests passed on 2026-09-21 (90 tests across suites); see the detailed report for actual run dates and evidence. No remote CI run or production deployment is claimed.

Phase 05 delivers supplier profiles and branch ledgers, versioned multi-line drafts, exact landed allocation, atomic/idempotent receipts and payable posting, partial cash allocations, payment/purchase reversals, linked replacements and authenticated PDF vouchers. Migration `20260919072004_Purchasing` must run before the updated API; it adds tables, constraints, immutable-history guards and owner permissions without recosting stock. Sales and the wider Phase 07 payments/cash module remain future work. The platform remains local development infrastructure. Private source inputs remain ignored. Production email, encrypted shared keys, coordinated rate limits and optional MFA remain explicit gates.

Toolchain: .NET SDK 10.0.401, runtime/EF 10.0.12, Node 24.13.1, pnpm 12.4.2 and PostgreSQL 18.4. Local database port is now 54320 because Windows reserved the previous 55432 port. The template default remains configurable at 55432. Bootstrap creates a random password in ignored `.env`; API startup never applies migrations automatically.

## Exact next prompt

Current (2026-09-28): Phase 07 is complete. When the owner requests Phase 08, open a new chat and use the reusable prompt in docs/PHASE_HISTORY.md with NN = 08. Do not start Phase 08 automatically.

Historical Phase 06 prompt: Finish Phase 06 verification and Git integration. Read the current Phase 06 report and preserve historical evidence. After required checks pass, merge to main, push and delete the merged feature branch. Do not start Phase 07.

Current Phase 06 kickoff baseline: 72 tests passed (11 unit, 4 architecture, 44 PostgreSQL/API, 13 frontend), plus frontend lint/types and documentation formatting/whitespace. No Phase 06 application implementation or new migration; browser/OpenAPI/Compose/PDF visual checks were not rerun. This was historical kickoff verification; see the Phase 06 report for later implementation checks.

## Historical Phase 06 draft checkpoint verification

Current incomplete checkpoint: 17 unit, 4 architecture, 51 PostgreSQL/API, 13 frontend and 20 desktop/mobile browser cases passed across the runs detailed in [the Phase 06 report](docs/PHASE_06_REPORT.md), 105 total. Frontend lint/types/build, OpenAPI, fresh/upgrade/repeat migrations, model drift, Compose health, formatting, links and whitespace passed. The feature is not complete and is not merged; no remote CI or push is claimed. Customers, prices and draft preparation are usable locally. Posting/receipts/payments, custody/deposits/exchanges and the purchase-cost correction remain outstanding. Historical kickoff verification above is separate.

Verified Phase 06 partial implementation checkpoint: `51b340a` on `feature/phase-06-sales`. Working tree was clean after that commit. No main merge, branch deletion or remote push occurred for the incomplete phase.

## Phase 06 final verification in progress — 2026-09-25

The full implementation now includes sales posting, all sale modes, receipts, payments, cylinder loans/deposits/returns and separate capitalized purchase-cost payments. The complete backend run passed 95 tests before the latest empty-policy JSON correction; final browser and correction checks are pending. Earlier incomplete-checkpoint paragraphs are historical. Five Phase 06 migrations preserve existing stock and posted purchasing treatment. See the detailed report for migration, verification and operating limits. Changes are currently uncommitted; merge/push/deletion have not occurred. Finish these checks and integration; do not start Phase 07.

## Completion checkpoint — 2026-09-26

Phase 06 implementation and required local checks are complete. Final browser report: 20/20 passed, no flaky or skipped cases. Verification covers 30 unit, 4 architecture, 61 PostgreSQL/API, 13 frontend and 20 browser tests (128 distinct cases across the recorded runs), with all 17 sales/cylinder integration cases rerun after the final endpoint correction. Formatting, lint, types, production builds, migration fresh/upgrade/repeat and model checks, OpenAPI, Compose health and PDF review evidence are detailed in the phase report. Historical results are distinguished from final reruns there.

Approved scope includes variable worker-entered usage fees, controlled deposit waivers and damage/loss approval, separate deposit liability and actual refunds, filling loss per line, and separately paid purchase costs capitalized into stock. Source/customer cylinder references are supported; per-source physical gas balances remain a documented future decision. Existing posted history is preserved. Five additive Phase 06 migrations must precede API startup; no production deployment or remote CI success is claimed.

Acceptance: implementation meets the recorded Phase 06 checks and approved operating rules. The owner authorized integration after verification; Git completion will be recorded below. Exact next-chat prompt: Review the completed Phase 06 checkpoint in PROJECT_STATE.md, docs/PHASE_HISTORY.md and docs/PHASE_06_REPORT.md. Address only requested Phase 06 follow-up work; do not begin Phase 07 unless explicitly requested.

## Git integration completed — 2026-09-26

Verified implementation checkpoint: `3a7d055`. Merge commit: `d222c75` on `main`, pushed successfully to origin. The merged local `feature/phase-06-sales` branch was deleted; no remote feature branch existed. The secondary detached worktree and its files were preserved. Phase 06 is complete; Phase 07 has not started. The next action is only an explicitly requested follow-up or new phase.

## Active Phase 07 continuation — 2026-09-28

Continue Phase 07 in this chat from the uncommitted feature branch. Complete finance integration, screens, guards and tests; resolve operating decisions; run all quality gates and update all three checkpoint records. Merge to main, push and delete the feature branch only after completion. Do not repeat Phase 06 or start Phase 08.

## Phase 07 completion checkpoint — 2026-09-28

Implementation and required local checks are complete. Run in this chat: 4 architecture, 40 unit and 72 PostgreSQL/API tests (116), 14 frontend tests and 22/22 desktop/mobile browser tests on rebuilt containers. Build with 0 warnings, formatting, lint, types, production build, OpenAPI, fresh/upgrade/repeat migrations and model drift, Compose health, and pnpm/NuGet audits also passed. Corrections made during verification: clear register-guard error messages, accessible finance select labels, and split auth rate limits (strict per-IP credential attempts; separate per-user budget for read-only session checks). See the [Phase 07 report](docs/PHASE_07_REPORT.md). Apply migration `20260928120055_FinanceAndRegisters` before API startup. No remote CI or production deployment is claimed.

Toolchain note: .NET SDK 10.0.401 is installed user-locally at `%LOCALAPPDATA%\Gas365\dotnet`. Prepend it to PATH for builds. Use `npx pnpm@12.4.2` if pnpm is not on PATH.

## Phase 07 Git integration completed — 2026-09-28

Verified implementation checkpoint: `bbd39ee`. Merge commit: `2eb81ba` on `main`, pushed successfully to origin. The merged local `feature/phase-07-finance` branch was deleted; no remote feature branch existed. The secondary detached worktree was preserved. Phase 07 is complete; Phase 08 has not started.

## Phase 07 follow-up — 2026-09-29

Advances can settle cylinder charges, and accounts and expense categories can be renamed, retired and reactivated. Both limitations are closed; see the [Phase 07 report](docs/PHASE_07_REPORT.md). Apply migration `20260929072843_FinanceMasterDataAndCylinderAllocation` before API startup. The per-category cash reconciliation is carried into Phase 08.

Owner direction on 2026-09-29: continue through the remaining phases in this chat, one feature branch per phase, merging only after all checks pass. This explicitly supersedes the one-phase-per-chat default for this run. Commit messages must not mention tools or carry attribution IDs.
