# Source inventory

Reviewed 2026-09-16. Original materials reside locally under `Gas365-Development-Pack/deliverables/Gas365-Development-Pack/`. They are excluded from source control. The root master prompt duplicated the pack's master prompt. The initial workspace contained 14 Markdown files, one workbook and one ZIP archive, with no application code.

## Written sources

The pack includes the master prompt, repository rules, project state, README, phase prompts and reference documents for product scope, architecture/security, business rules, UI, local setup, source review, DevOps/testing/reporting/AI, plus an input README. These were read in full. The maintained reference copies now live in `reference/`; the original pack is archival rather than authoritative for project progress.

## Workbook

`Royal_LPG_September_2026_ERP.xlsx` contains 47 worksheets. Worksheet XML, labels and formula presence were inspected across all sheets. This was structural and requirements analysis, not an audit or recalculation of financial results.

| Worksheets                                                     | Observed purpose                                                               |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| Sales Overview                                                 | Search/filter sales, quantities, prices, paid amounts and balances             |
| Expenses Overview; Major Expenses                              | Operating and major expenditure registers                                      |
| Loan Overview; LOANS                                           | Outstanding customer amounts and later collections against original sale IDs   |
| Spare Parts Purchase; Spare Parts Inventory & Profit; _SP_FIFO | Purchases, landed costs, supplier balances, stock and FIFO helper calculations |
| Lists                                                          | Editable choices and calculation helpers                                       |
| LPG Purchases; LPG Profit                                      | LPG receipts, landed cost, load profitability and FIFO analysis                |
| Stock Adjustments                                              | Returns and non-cash settlements against receivables                           |
| Sep 01 through Sep 30                                          | Daily sales, price/quantity entry, payments, expenses and cash summaries       |
| Weekly Summary; Monthly Summary                                | Period aggregation                                                             |
| Inventory                                                      | LPG and physical product stock reconciliation                                  |
| Guide                                                          | Entry guidance and iterative price/quantity calculation behavior               |
| _ReportData                                                    | Consolidated report helper data                                                |

`_SP_FIFO` and `_ReportData` are very hidden worksheets. The Guide refers to an optional VBA text file that was not supplied. The supplied workbook filename differs from the input README's expected `(10)` suffix; the available file is the reviewed source.

## Screenshot archive

`GasFlow Inspiration.zip` contains **142 PNG files**, not the 143 stated in the original review. All images were inventoried and visually reviewed in contact sheets. Small field text and invisible behavior were not treated as verified implementation details.

The following ranges refer to archive order, with screenshots named `Screenshot 2026-09-15 <time>.png`:

| Images  | Observed workflows                                          |
| ------- | ----------------------------------------------------------- |
| 1–16    | Dashboard, business profile, branches and team              |
| 17–35   | Refill calculator entry point, stock and spare parts        |
| 36–49   | Purchases, line entry, payments and voucher                 |
| 50–67   | Sales, receipt, customer entry and sales history            |
| 68–79   | Expenses, credit book and Udhaar overview                   |
| 80–87   | Partial-payment sale and customer outstanding profile       |
| 88–102  | Cash open/close/reopen and supplier management              |
| 103–115 | Business reports, profit/loss, spare parts and cash history |
| 116–121 | Account settings and subscription billing                   |
| 122–142 | Public marketing, registration, login and plan selection    |

## Cross-source gaps and contradictions

| Finding                                                                                      | Resolution or required decision                                                                                              |
| -------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Workbook uses FIFO; business specification selects weighted average                          | Follow [ADR 004](adr/004-costing.md); do not expect historic FIFO profit to equal new weighted-average profit                |
| Workbook LOANS mostly represents customer collections                                        | Model receivables separately from actual borrowed principal and owner funds                                                  |
| Workbook offers circular price/quantity entry                                                | Require an explicit input mode and deterministic server calculation; rounding policy remains open                            |
| Stock adjustments settle debt without cash                                                   | Separate inventory return effects, receivable credits and cash effects; require explicit settlement rules                    |
| Screenshots expose editing/deleting controls                                                 | They do not establish safe posting semantics; use [ADR 005](adr/005-corrections.md)                                          |
| Supplier payments, tenant isolation, concurrency and backups cannot be proved by screenshots | Treat specification controls as requirements with negative/invariant tests                                                   |
| Delivery appears in product scope but has no dedicated implementation phase                  | Delivery dispatch, assignment and proof of delivery require scope approval before implementation                             |
| Offline capability appears in inspiration marketing                                          | Full offline transaction synchronization remains outside MVP                                                                 |
| “12 stages” lists 00–12                                                                      | Maintained blueprint now identifies 13 numbered stages                                                                       |
| Pilot is described both after Stage 08 and in Stage 12                                       | Stage 08 is functional MVP; real-data deployment requires applicable security/restore gates; Stage 12 is the managed rollout |

No third-party code or assets are part of the implementation. External claims in the original source review have not been independently verified and are not architectural evidence.
