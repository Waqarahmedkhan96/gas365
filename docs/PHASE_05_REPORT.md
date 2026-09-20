# Phase 05 checkpoint

Status: implemented and locally verified on 2026-09-21. Work began 2026-09-19. Implementation checkpoint: `a3b75a0`, including backend commit `8e2d9f8`, on `codex/phase-05-purchasing`. Business acceptance of the two operating choices below remains open. Phase 06 has not started.

## Scope and acceptance criteria

Suppliers, purchase drafts and multi-line receipts, visible landed-cost allocation, atomic/idempotent stock and payable posting, partial cash payments, immutable corrections, supplier ledger/profile screens and PDF vouchers. Required acceptance: stock/payable/cash reconciliation, permission/tenant/branch isolation, concurrency and retry safety, immutable history, fresh/upgrade migrations, backend/frontend/browser checks, OpenAPI, Compose health and documentation.

## Delivered implementation

Domain decimal allocation; application DTOs; purchasing services using the existing tenant stock lock; supplier/draft/posted document/payment schema and migration; append-only guards; API endpoints; lazy-loaded purchase and supplier pages with server totals and pagination; authenticated PDFsharp vouchers. See [purchasing contracts](PURCHASING.md).

Phase 04 costing remains as approved on 2026-09-19. Implementation choices awaiting confirmation are supplier-paid landed charges and separate reversal of payments before purchase reversal. Cash-only invoice allocations are Phase 05; bank accounts, advances and cash closing remain Phase 07. No production deployment.

## Current Phase 05 verification

- On 2026-09-20, backend Release build and publish passed. **11 unit, 4 architecture and 44 PostgreSQL/API tests passed.** The complete backend run passed 9 unit/4 architecture/44 integration tests; the unit suite was then rerun with two additional PDF pagination cases and passed all 11. No tests were skipped.
- Eight purchasing integration cases cover allocation/stock/payable/cash reconciliation, exact reversal and linked replacement, same-key concurrent posting and conflicting reuse, overpayment and stale payment detection, concurrent draft edit/post, late serial-validation rollback, immutable database records, supplier versions, foreign references, every new route's workspace/permission boundary, branch grants and read-only role restrictions. Inventory's reversal route rejects purchase receipts.
- Fresh migrations, upgrade through the actual Phase 04 schema, retained stock quantities/values/costs and owner permissions, repeat migration application and no-pending-model-change checks passed on 2026-09-20. No prior stock was recosted.
- Frontend lint, type check, **13 component tests** and production image build passed on 2026-09-20. All **18 desktop/mobile browser tests passed** on 2026-09-21, including two-decimal PKR display, lost-response retry, partial payments, full correction/replacement, supplier ledger, PDF download, accessibility and viewport checks. Total across suites: **90 passing tests**. Frontend lint and type check were also rerun successfully on 2026-09-21.
- OpenAPI validation passed and saved `artifacts/openapi.json`. JavaScript advisory audit reported no known vulnerabilities; NuGet advisory validation passed, including PDFsharp. Compose built the images and completed its migration job. PostgreSQL, Mailpit, API and web were healthy on 2026-09-20 and rechecked healthy on 2026-09-21.
- PDF tests generated a one-page, two-line fixture and a 17-page, 100-line fixture with long names/references. Poppler rendered every page; visual inspection of the short voucher and all long-voucher pages found no clipping or overlap. The font license is included in publish output. Vouchers are authenticated, no-store, generated in memory and audited.
- Backend formatting and repository Prettier checks passed again on 2026-09-21. All 84 local Markdown links in root, docs and prompts files resolve; `git diff --check` passed. No remote CI run is claimed. Implementation commits were subsequently verified as `8e2d9f8` and `a3b75a0`.

Evidence is in ignored `artifacts/test-results/`, `artifacts/pdf-qa/`, `artifacts/nuget-audit.json`, `artifacts/openapi.json`, `test-results/` and `playwright-report/`. CI remains configured to repeat the checks; no checkpoint was pushed or remote CI run in this task.

## Failures investigated and corrections

- The initial sandbox run could not access ASP.NET Data Protection keys and failed authentication setup. The 2026-09-20 unsandboxed backend run passed all integration assertions. The interrupted earlier rerun is not counted as evidence.
- Docker Desktop was stopped at the 2026-09-20 resumption. Existing local services/volumes were reused after starting Docker. No business data was deleted or imported.
- Windows' bundled pnpm `exec` wrapper did not resolve installed executables. Equivalent local Node CLI commands ran Playwright and Prettier. Host pnpm reports 11.19.0; frozen-lockfile image builds use pinned pnpm 12.4.2.
- Browser tests exposed select-label matching and required-label suffix mismatches; selectors now have explicit labels and test locators target the actual labels. Assertions were not relaxed.
- A mobile accessibility scan found the shared shell's business identity outside a landmark. Phase 05 corrects it to a named region. This is a later correction to Phase 03, not a rewrite of its historical verification.
- The browser's PKR default omitted fractional digits. Purchasing now explicitly displays the currency code and two decimal places. A repeated full run also hit the unchanged authentication limiter (429 on `/api/auth/me`); no authentication or CSRF protection was weakened. Live setup helpers now reuse CSRF tokens within each identity and refresh after login; the browser suite uses one worker because all live journeys share one client IP. The final complete run passed 18/18 without retries.

## Historical evidence

Phase 04's 73 passing checks belong to the 2026-09-18 implementation checkpoint `7d58917`; they are not current Phase 05 verification. Existing Phase 04 acceptance/handoff edits were already uncommitted when this task started and are preserved.

## Migration impact

`20260919072004_Purchasing` adds purchasing tables, tenant-qualified constraints, posted-history guards and owner purchasing permissions. No inventory recosting or business data seeding. Apply before API startup. Downgrade destroys purchasing data; recovery requires backup/restore.

## Limits and acceptance

One purchase belongs to one branch and supplier. Cash payments are allocated to one invoice, with no advances, bank accounts, aging or closed-day cash controls. Freight charges are included in the same supplier's payable. Payment reversal records a correction or cash return; it does not send money. Full purchase reversal is blocked by later affected stock or serial movements. Taxes, legal invoice configuration, Urdu PDF shaping and production infrastructure remain open or later-phase scope. English vouchers use a redistributable Latin font.

Engineering verification does not imply approval of business assumptions. Owner review remains open for (1) landed charges payable to the purchase supplier, with separate freight bills deferred, and (2) separate payment reversal before purchase reversal. Unrelated pre-existing documentation changes are preserved. The Phase 05 implementation checkpoint is `a3b75a0`.

## Exact next-chat prompt

Review the Phase 05 checkpoint only. Read AGENTS.md, PROJECT_STATE.md, docs/PHASE_HISTORY.md, docs/PHASE_05_REPORT.md, docs/PURCHASING.md and Prompt 05. Inspect Git status and preserve existing changes. Confirm or revise supplier-paid landed charges and separate payment reversal before purchase reversal; retain the approved Phase 04 costing policy. Apply only requested Phase 05 corrections, rerun affected checks, and update all three checkpoint records with acceptance and commit status. Do not start Phase 06.
