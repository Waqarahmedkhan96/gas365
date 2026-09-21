# Phase 06 checkpoint

Status: implementation in progress; customer/price-list/sales-draft checkpoint only. Full Phase 06 is not complete or accepted.

## Scope and acceptance plan

1. Resolve prior purchasing choices and cylinder sales/exchange/deposit rules; preserve approved Phase 04 costing.
2. Implement customers, server-owned price lists, multi-line sales, payment capture, immutable snapshots, atomic/idempotent stock/receivable/revenue/COGS posting and reasoned reversals.
3. Add responsive sales register/detail, customer history and authenticated receipt PDF.
4. Verify calculations, reconciliation, all sale modes, invalid states, permissions, tenant/branch isolation, concurrency/retry, fresh/upgrade migrations, frontend/browser journeys, PDF rendering, OpenAPI and Compose health.
5. Update durable records, merge completed and verified work into main, delete its feature branch and stop before Phase 07.

## Repository integration

Verified purchasing implementation commits `8e2d9f8` and `a3b75a0`, with documentation through `65d3ecf`. Both worktrees were clean before integration. Merge `a97fe9b` preserves purchasing and the existing Phase 04 acceptance commit `4304318`. Documentation conflicts were resolved to retain the latest phase status and historical acceptance. The merged purchasing branch was deleted after detaching its clean worktree; its files remain intact. Active branch: `feature/phase-06-sales`. No remote push.

## Pending decisions

The owner has now clarified:

- Petrol, tolls, loading and unloading are separately paid operating outlays, including use of the business's own vehicle; they are not owed to the goods supplier. Correct the purchasing implementation accordingly, without rewriting posted history.
- Keep separate reasoned payment reversal before purchase reversal.
- Include borrowed cylinders and refundable deposits in Phase 06.

Two follow-up questions remain: stock capitalization versus immediate expense for purchase-related outlays, and deposit/waiver/return/damage/loss terms. No response has yet been recorded. Detailed gas and returned-shell valuation rules also need explicit documentation before posting implementation.

Acceptance now also requires customer-specific cylinder custody reconciliation, deposit liability separate from sales revenue, controlled deposit collection/refund, and stock/payment effects that reverse together without modifying original history. The purchasing correction must separate the goods supplier balance from separately paid transport/loading costs and retain historical document semantics.

## Delivered implementation checkpoint

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

## Implementation verification (current work)

Current implementation verification on 2026-09-21:

- Release build: 17 unit, 4 architecture and 51 real PostgreSQL/API tests passed, none skipped. Seven new integration tests cover customer/price/draft isolation, decimal totals, invalid references, inactive configuration, stale prices and edits, live permissions, concurrent draft edits, branch restrictions and absence of draft stock/payment effects.
- Frontend lint, type checking, 13 component tests and production build passed.
- Browser regression run passed the 18 existing desktop/mobile journeys. The two new journeys initially failed at the select label; after correcting the markup, both desktop/mobile customer/price/draft journeys passed without retries. Coverage includes profile editing/search, six-place prices, price edits, multi-line draft totals/edits, axe checks and viewport overflow. Thus 20 browser cases passed across those runs; this is not a claim of a single final 20/20 invocation.
- Total: 105 passing tests across suites/runs. Fresh/Phase 05 upgrade/repeat migrations and model drift passed, retaining historical stock values and suppliers. Compose rebuilt production images, completed the migration job and reported PostgreSQL, Mailpit, API and web healthy.
- OpenAPI validation passed with new endpoint assertions. Repository Prettier, backend formatting, 93 maintained Markdown links and Git whitespace checks passed. Two merge-era documents needed formatting only; historical evidence was retained.
- No remote CI, production deployment or new receipt PDF is claimed. Receipts are not implemented at this checkpoint.

Corrections during verification: moved price ordering/filtering before response projection for database translation; corrected a test's owner-role lookup; restored stopped local services without deleting volumes; separated the sale-mode label from its select; mapped nested PostgreSQL serialization/deadlock conflicts to 409. The competing edit remains rejected; no assertions or authorization controls were weakened.

Migration impact: `20260921002027_CustomersAndPriceLists` and `20260921070611_SalesDrafts` add four tables, tenant-qualified constraints, indexes and owner permissions. Upgrade verification retains prior inventory values and supplier records; no posted purchases are rewritten and no stock is recosted. Fresh and repeated application/model drift are checked by the integration suite. Downgrade would destroy the new records; use backup/restore for recovery. Implementation checkpoint verified in Git: `51b340a` on `feature/phase-06-sales`. It is intentionally not merged into main because the phase is incomplete.
