# Phase 06 checkpoint

Status: preparation started 2026-09-21; not implemented or accepted.

## Scope and acceptance plan

1. Resolve prior purchasing choices and cylinder sales/exchange/deposit rules; preserve approved Phase 04 costing.
2. Implement customers, server-owned price lists, multi-line sales, payment capture, immutable snapshots, atomic/idempotent stock/receivable/revenue/COGS posting and reasoned reversals.
3. Add responsive sales register/detail, customer history and authenticated receipt PDF.
4. Verify calculations, reconciliation, all sale modes, invalid states, permissions, tenant/branch isolation, concurrency/retry, fresh/upgrade migrations, frontend/browser journeys, PDF rendering, OpenAPI and Compose health.
5. Update durable records, merge completed and verified work into main, delete its feature branch and stop before Phase 07.

## Repository integration

Verified purchasing implementation commits `8e2d9f8` and `a3b75a0`, with documentation through `65d3ecf`. Both worktrees were clean before integration. Merge `a97fe9b` preserves purchasing and the existing Phase 04 acceptance commit `4304318`. Documentation conflicts were resolved to retain the latest phase status and historical acceptance. The merged purchasing branch was deleted after detaching its clean worktree; its files remain intact. Active branch: `feature/phase-06-sales`. No remote push.

## Pending decisions

The prior report explicitly requires confirmation of supplier-paid landed charges and separate payment reversal before purchase reversal. Questions were presented to the owner; no answer has been recorded. A second question proposes ownership sales and compatible equal exchanges, deferring deposits/customer cylinder loans. This proposal is not approval. Detailed gas and returned-shell valuation rules must be resolved before posting implementation.

## Checks and migration impact

Git whitespace check passed during conflict resolution. No application changes or new migrations in this kickoff. Historical Phase 05 results (90 tests) are in its report and are not new results. Current merged-baseline checks passed on 2026-09-21: Release build and 11 unit, 4 architecture, 44 PostgreSQL/API and 13 frontend tests (72 total, none skipped). Frontend type checking and lint passed. Initial backend attempts could not run with the default SDK and then encountered package-configuration permissions; the installed pinned SDK and an elevated run resolved those environment issues. Browser, PDF visual, OpenAPI and Compose checks were not rerun in this kickoff. No Phase 06 behavior is claimed by these baseline tests. Documentation formatting and Git whitespace passed. The kickoff documentation checkpoint is recorded in Git; the feature remains incomplete.

## Exact continuation prompt

Continue Phase 06 in this task. Resolve the pending business questions, inspect current status, implement the scope above and run required checks. Update this report, the combined history and project state with actual evidence. Merge and delete the feature branch only after completion and verification. Do not start Phase 07.
