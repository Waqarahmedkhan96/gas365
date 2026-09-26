# Phase 06 checkpoint

Status: implementation complete and locally verified; Git integration is the final step. Earlier sections below preserve historical checkpoints; the completion addendum supersedes their outstanding-work lists.

## Scope and acceptance plan

1. Resolve prior purchasing choices and cylinder sales/exchange/deposit rules; preserve approved Phase 04 costing.
2. Implement customers, server-owned price lists, multi-line sales, payment capture, immutable snapshots, atomic/idempotent stock/receivable/revenue/COGS posting and reasoned reversals.
3. Add responsive sales register/detail, customer history and authenticated receipt PDF.
4. Verify calculations, reconciliation, all sale modes, invalid states, permissions, tenant/branch isolation, concurrency/retry, fresh/upgrade migrations, frontend/browser journeys, PDF rendering, OpenAPI and Compose health.
5. Update durable records, merge completed and verified work into main, delete its feature branch and stop before Phase 07.

## Repository integration

Verified purchasing implementation commits `8e2d9f8` and `a3b75a0`, with documentation through `65d3ecf`. Both worktrees were clean before integration. Merge `a97fe9b` preserves purchasing and the existing Phase 04 acceptance commit `4304318`. Documentation conflicts were resolved to retain the latest phase status and historical acceptance. The merged purchasing branch was deleted after detaching its clean worktree; its files remain intact. Active branch: `feature/phase-06-sales`. No remote push.

## Historical pending decisions

The owner has now clarified:

- Petrol, tolls, loading and unloading are separately paid operating outlays, including use of the business's own vehicle; they are not owed to the goods supplier. Correct the purchasing implementation accordingly, without rewriting posted history.
- Keep separate reasoned payment reversal before purchase reversal.
- Include borrowed cylinders and refundable deposits in Phase 06.

At the earlier checkpoint, two follow-up questions were pending. The owner has now approved the deposit/return recommendations, with a variable usage fee entered by workers on return. Stock capitalization versus immediate expense for purchase-related outlays remains unanswered. Detailed gas and returned-shell valuation rules also need explicit documentation before posting implementation.

Acceptance now also requires customer-specific cylinder custody reconciliation, deposit liability separate from sales revenue, controlled deposit collection/refund, and stock/payment effects that reverse together without modifying original history. The purchasing correction must separate the goods supplier balance from separately paid transport/loading costs and retain historical document semantics.

## Historical draft implementation checkpoint

- Tenant-owned searchable customer contacts with active status, versioned edits and audit events.
- Named price lists, item/mode price validation, six-place decimal unit prices, deactivation and stale/duplicate protection.
- Branch-scoped multi-line sales drafts using server-read price versions, exact rounded gross/discount/net and a separate discount permission. Saved snapshots do not change when current prices change. No draft has financial or stock effects.
- Responsive customer, price-list and draft screens; explicit view/edit permissions; catalog metadata access without granting stock-cost access.
- Tenant-qualified database constraints, central filters, optimistic concurrency and consistent draft snapshots. Concurrent transaction conflicts return 409 without automatic retry.

See [sales contracts](SALES.md). Full posting/idempotency, COGS/profit, receipts, payment capture, sales reversals, customer transaction history, cylinder exchanges/loans/deposits and separately paid purchase costs remain outstanding. Preparing a cylinder price or draft does not implement cylinder movements. Do not merge this incomplete phase into main.

## Checks and migration impact

Historical kickoff: Git whitespace check passed during conflict resolution. At that point there were no application changes or new migrations. Historical Phase 05 results (90 tests) are in its report and are not new results. Current merged-baseline checks passed on 2026-09-21: Release build and 11 unit, 4 architecture, 44 PostgreSQL/API and 13 frontend tests (72 total, none skipped). Frontend type checking and lint passed. Initial backend attempts could not run with the default SDK and then encountered package-configuration permissions; the installed pinned SDK and an elevated run resolved those environment issues. Browser, PDF visual, OpenAPI and Compose checks were not rerun in this kickoff. No Phase 06 behavior is claimed by these baseline tests. Documentation formatting and Git whitespace passed. The kickoff documentation checkpoint is recorded in Git; the feature remains incomplete.

## Exact continuation prompt

Continue Phase 06 in this task. Resolve the pending business questions, inspect current status, implement the scope above and run required checks. Update this report, the combined history and project state with actual evidence. Merge and delete the feature branch only after completion and verification. Do not start Phase 07.

## Historical draft implementation verification

Current implementation verification on 2026-09-21:

- Release build: 17 unit, 4 architecture and 51 real PostgreSQL/API tests passed, none skipped. Seven new integration tests cover customer/price/draft isolation, decimal totals, invalid references, inactive configuration, stale prices and edits, live permissions, concurrent draft edits, branch restrictions and absence of draft stock/payment effects.
- Frontend lint, type checking, 13 component tests and production build passed.
- Browser regression run passed the 18 existing desktop/mobile journeys. The two new journeys initially failed at the select label; after correcting the markup, both desktop/mobile customer/price/draft journeys passed without retries. Coverage includes profile editing/search, six-place prices, price edits, multi-line draft totals/edits, axe checks and viewport overflow. Thus 20 browser cases passed across those runs; this is not a claim of a single final 20/20 invocation.
- Total: 105 passing tests across suites/runs. Fresh/Phase 05 upgrade/repeat migrations and model drift passed, retaining historical stock values and suppliers. Compose rebuilt production images, completed the migration job and reported PostgreSQL, Mailpit, API and web healthy.
- OpenAPI validation passed with new endpoint assertions. Repository Prettier, backend formatting, 93 maintained Markdown links and Git whitespace checks passed. Two merge-era documents needed formatting only; historical evidence was retained.
- No remote CI, production deployment or new receipt PDF is claimed. Receipts are not implemented at this checkpoint.

Corrections during verification: moved price ordering/filtering before response projection for database translation; corrected a test's owner-role lookup; restored stopped local services without deleting volumes; separated the sale-mode label from its select; mapped nested PostgreSQL serialization/deadlock conflicts to 409. The competing edit remains rejected; no assertions or authorization controls were weakened.

Migration impact: `20260921002027_CustomersAndPriceLists` and `20260921070611_SalesDrafts` add four tables, tenant-qualified constraints, indexes and owner permissions. Upgrade verification retains prior inventory values and supplier records; no posted purchases are rewritten and no stock is recosted. Fresh and repeated application/model drift are checked by the integration suite. Downgrade would destroy the new records; use backup/restore for recovery. Implementation checkpoint verified in Git: `51b340a` on `feature/phase-06-sales`. It is intentionally not merged into main because the phase is incomplete.

## Approved cylinder deposit and usage policy

The owner accepted the recommendations in this task and clarified that workers enter the usage charge manually on return. It may differ each time; PKR 2,000 was an example, not a fixed or monthly rate. No automatic duration-based charge is authorized.

- Configure a suggested deposit per cylinder type, but permit manual non-negative deposit amounts, including zero, subject to permissions. Reductions/waivers require authorized staff; admin/store-manager-approved zero-deposit customer arrangements support hotels and tandoors.
- Borrowed cylinders remain business-owned and are tracked against the customer even when no deposit is collected. Return moves the cylinder to empty stock or inspection according to condition, without treating the shell as a new purchase.
- A worker with return/usage-entry permission can enter the variable usage fee when processing the return. Do not require manager entry or approval of an ordinary usage fee merely because damage/loss charges need approval. Record actor, amount and return reference; show fee terms to the customer when issuing the cylinder.
- Damage or loss charges require admin/store-manager approval; ordinary workers cannot approve them. Permission is checked by the API, not inferred from a client role label.
- Deposit collection increases cash and refundable deposit liability, not sales income. The earned usage fee is separate income; applying it against the deposit reduces the liability. Refundable deposits are shown in cash received, separate from revenue/profit.
- Return settlement displays original deposit, usage fee, approved damage/loss charges and refund due. Only actual payment records a cash refund; an unpaid refund remains owed. Approved charges beyond a deposit, including a zero-deposit loan, must be collected separately rather than producing a negative refund.
- All cash, fee, custody and refund effects need atomic/idempotent posting and auditable reversal. This is an approved policy, not a claim that the workflows are implemented.

This clarification changes documentation only. No code/schema changes or application tests were run for it; the 105-test implementation result above remains historical evidence for `51b340a`.

## Full implementation addendum — 2026-09-25

This addendum supersedes the incomplete scope and pending decisions in the historical checkpoints above.

Delivered: atomic, idempotent posted sales with immutable prices/COGS; refill, full-cylinder, shell-only, exchange, spare-part and borrowed-cylinder modes; partial cash payments and reasoned reversals; branch-scoped registers/customer history; authenticated receipt PDFs. Custom filling supports configured kilogram precision and a manual loss/reason for each line, shown in red with a text label. Optional source and customer cylinder references provide traceability; source-cylinder gas capacity reconciliation is not implemented.

Cylinder policies configure separate LPG stock and kilograms per full cylinder, suggested deposits and usage terms. Exchanges receive an empty matching shell and issue a full shell without changing shell value; LPG is deducted once. Borrowed shells remain owned stock. Authorized staff control deposit reductions and zero-deposit customer arrangements. Workers enter variable usage fees on return; damage/loss charges require a separate approval permission. Deposit liability is separate from revenue; actual cash refunds and excess-charge collections are separate auditable actions. Concurrent returns cannot duplicate settlement.

The owner selected capitalization of separately paid purchase-related costs. New purchases retain allocated stock cost but exclude those costs from supplier payable. Separate cash payments and reversals track these outlays. Historical purchases retain their original totals and treatment.

Migration impact: the two earlier customer/draft migrations plus PostedSales, SeparatePurchaseCosts and CylinderLoans bring the database to ten migrations. Additive tables, tenant-qualified references, immutable-history guards and owner permissions are included. Existing stock is not recosted. Upgrade tests preserve historical supplier-paid purchases. Apply migrations before API startup; use backup/restore rather than destructive downgrades.

Verification before the latest endpoint correction: 30 unit, 4 architecture and 61 PostgreSQL/API tests passed in one Release run, none skipped. Earlier frontend checks passed 13 tests, lint and types; production Compose health, OpenAPI, fresh/upgrade/repeat migrations and model drift passed. Four PDF fixtures (36 pages total) were rendered and visually reviewed, including long-name pagination. These results are historical to the final rerun below, not a claim that the latest source has passed every gate.

Browser verification found an empty JSON response when cylinder policy was not yet configured. The endpoint now explicitly returns JSON null, with an integration assertion; required-field browser locators were also corrected. Final regression verification is pending. Previous approval-system usage-limit failures prevented the rerun; no business authorization was missing.

Boundaries: English operational PDFs are not tax invoices; no bank/cash-register closing, advances, automatic monthly usage fee, source-cylinder gas subledger or automatic lost-shell asset write-off. Lost shells remain unavailable under the approved inventory-state valuation policy. Optional source/customer references were the stated fallback to the unanswered tracking clarification. No production deployment or remote CI result is claimed.

Acceptance status: implemented, awaiting final checks and Git integration. Current changes are uncommitted; verified earlier partial checkpoint is 538f743. Exact continuation: finish Phase 06 checks, update all three records, commit, merge into main, push and delete the merged feature branch. Do not start Phase 07.

## Final correction checks — 2026-09-25

The corrected source compiled successfully. Backend formatting passed, and all 17 customer/sales/cylinder PostgreSQL integration tests passed, including the missing-policy JSON assertion. This rerun is additional evidence for the previously passing complete 95-test backend suite; overlapping tests are not counted twice. Frontend lint, type checking and 13 component tests passed again. Compose rebuilt the production images, applied/repeated migrations and reported API, web, PostgreSQL and Mailpit healthy. OpenAPI validation passed. Repository Prettier and 92 maintained relative Markdown links passed. Both expanded desktop/mobile sales, loss, borrowing, usage-fee and refund journeys passed; the final full browser report confirms 20 expected passes, zero unexpected failures, zero flaky tests and zero skips.

## Completion checkpoint — 2026-09-26

Phase 06 implementation and required local checks are complete. Final browser report: 20/20 passed, no flaky or skipped cases. Verification covers 30 unit, 4 architecture, 61 PostgreSQL/API, 13 frontend and 20 browser tests (128 distinct cases across the recorded runs), with all 17 sales/cylinder integration cases rerun after the final endpoint correction. Formatting, lint, types, production builds, migration fresh/upgrade/repeat and model checks, OpenAPI, Compose health and PDF review evidence are detailed in the phase report. Historical results are distinguished from final reruns there.

Approved scope includes variable worker-entered usage fees, controlled deposit waivers and damage/loss approval, separate deposit liability and actual refunds, filling loss per line, and separately paid purchase costs capitalized into stock. Source/customer cylinder references are supported; per-source physical gas balances remain a documented future decision. Existing posted history is preserved. Five additive Phase 06 migrations must precede API startup; no production deployment or remote CI success is claimed.

Acceptance: implementation meets the recorded Phase 06 checks and approved operating rules. The owner authorized integration after verification; Git completion will be recorded below. Exact next-chat prompt: Review the completed Phase 06 checkpoint in PROJECT_STATE.md, docs/PHASE_HISTORY.md and docs/PHASE_06_REPORT.md. Address only requested Phase 06 follow-up work; do not begin Phase 07 unless explicitly requested.
