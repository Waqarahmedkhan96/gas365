# Gas365 project state

## Current phase

Phase 06 — Customers, Sales, Exchanges and Receipts: implementation started on 2026-09-21 at the owner's request, on `feature/phase-06-sales`. Purchasing was merged into local `main` at `a97fe9b` and its merged branch deleted. Customer records, selling-price lists and versioned multi-line sales drafts are implemented as an incomplete checkpoint. The server validates price versions, decimal totals, discounts and branch access. Two additive migrations exist. Posting, payment capture, PDF receipts, exchanges, borrowed-cylinder custody, deposits and the purchasing-cost correction remain outstanding; pending business policies must be resolved before dependent implementation. See [Phase 06 report](docs/PHASE_06_REPORT.md).

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
- All cash, fee, custody and refund effects need atomic/idempotent posting and auditable reversal. This is an approved policy, not a claim that the workflows are implemented.

## Open decisions

Owner decisions recorded during Phase 06: petrol, tolls, loading and unloading are paid separately from the goods supplier, including costs of the business's own vehicle. The previous supplier-paid landed-charge assumption is rejected and requires a purchasing correction. Separate reasoned payment reversal before purchase reversal is approved. Borrowed cylinders and refundable deposits are explicitly included in Phase 06, not deferred.

Remaining question: whether separately paid purchase-related costs are capitalized into stock or expensed immediately. Cylinder deposit and usage policy is now approved below; do not ask again whether the usage fee is fixed or monthly. Workers enter a variable fee on return. Gas consumption, returned-cylinder valuation and custody must be explicit before dependent posting rules are implemented. Existing posted purchasing history must remain unchanged; do not silently remove charges from historical supplier balances or recost inventory.

Cash-only allocations, no advances, and English PDF vouchers are existing boundaries; bank accounts/cash closing and full Urdu voucher typography remain later work. See [purchasing contracts](docs/PURCHASING.md).

See [risk register](docs/RISKS.md): legal entity/name/domain; tax/invoice/FBR applicability; production hosting, protected keys, SMTP and optional MFA/phone recovery; future return valuation and real-data cutover; cylinder customer/deposit relationships; payment advances and closed-period corrections; delivery scope; subscription provider; recovery objectives and production infrastructure. Provisioning defaults of 3 active branches, 10 users and 3 administrators are local development limits pending commercial plan decisions. Keep at least one active branch; owner transfer/removal is not exposed in this phase. Tenant locale configuration beyond browser locale remains open; dates use the existing tenant time zone. Inventory uses branch context and separately authorizes both source and destination branches.

## Verification and limitations

Historical Phase 04 verification: see [Phase 04 report](docs/PHASE_04_REPORT.md) and [inventory contracts](docs/INVENTORY.md). Its 73-test result belongs to 2026-09-18 and is not the current Phase 05 result.

Current Phase 05 verification (2026-09-20–21): backend Release build/publish, 11 unit, 4 architecture, 44 PostgreSQL/API and 13 frontend tests passed. Frontend lint/types/build, fresh and Phase 04 upgrade migrations, repeat application, model drift, OpenAPI, dependency audits and Compose health passed. PDF fixtures with 2 and 100 lines were rendered and visually checked across all pages. All 18 desktop/mobile browser tests passed on 2026-09-21 (90 tests across suites); see the detailed report for actual run dates and evidence. No remote CI run or production deployment is claimed.

Phase 05 delivers supplier profiles and branch ledgers, versioned multi-line drafts, exact landed allocation, atomic/idempotent receipts and payable posting, partial cash allocations, payment/purchase reversals, linked replacements and authenticated PDF vouchers. Migration `20260919072004_Purchasing` must run before the updated API; it adds tables, constraints, immutable-history guards and owner permissions without recosting stock. Sales and the wider Phase 07 payments/cash module remain future work. The platform remains local development infrastructure. Private source inputs remain ignored. Production email, encrypted shared keys, coordinated rate limits and optional MFA remain explicit gates.

Toolchain: .NET SDK 10.0.401, runtime/EF 10.0.12, Node 24.13.1, pnpm 12.4.2 and PostgreSQL 18.4. Local database port is now 54320 because Windows reserved the previous 55432 port. The template default remains configurable at 55432. Bootstrap creates a random password in ignored `.env`; API startup never applies migrations automatically.

## Exact next prompt

Continue Phase 06 in the existing task on `feature/phase-06-sales`. Read AGENTS.md, PROJECT_STATE.md, docs/PHASE_HISTORY.md, docs/PHASE_05_REPORT.md, docs/PHASE_06_REPORT.md, docs/PURCHASING.md, docs/INVENTORY.md and Prompt 06. Implement the approved cylinder deposit/return policy, including worker-entered variable usage fees and manager-approved damage/loss charges. Resolve the remaining purchase-cost treatment and document gas/exchange valuation before dependent work. Implement and verify Phase 06 only. Update the three checkpoint records; when complete and verified, merge into main and delete the feature branch. Do not start Phase 07.

Current Phase 06 kickoff baseline: 72 tests passed (11 unit, 4 architecture, 44 PostgreSQL/API, 13 frontend), plus frontend lint/types and documentation formatting/whitespace. No Phase 06 application implementation or new migration; browser/OpenAPI/Compose/PDF visual checks were not rerun. This was historical kickoff verification; see the Phase 06 report for later implementation checks.

## Phase 06 implementation verification

Current incomplete checkpoint: 17 unit, 4 architecture, 51 PostgreSQL/API, 13 frontend and 20 desktop/mobile browser cases passed across the runs detailed in [the Phase 06 report](docs/PHASE_06_REPORT.md), 105 total. Frontend lint/types/build, OpenAPI, fresh/upgrade/repeat migrations, model drift, Compose health, formatting, links and whitespace passed. The feature is not complete and is not merged; no remote CI or push is claimed. Customers, prices and draft preparation are usable locally. Posting/receipts/payments, custody/deposits/exchanges and the purchase-cost correction remain outstanding. Historical kickoff verification above is separate.

Verified Phase 06 partial implementation checkpoint: `51b340a` on `feature/phase-06-sales`. Working tree was clean after that commit. No main merge, branch deletion or remote push occurred for the incomplete phase.
