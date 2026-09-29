# Reports, dashboard and exports

Phase 08 adds server-calculated reports, a dashboard and private CSV, XLSX and PDF exports. The browser only formats values; every total comes from the posted ledgers in PostgreSQL. Each report has a definition, a filter contract and a reconciliation rule, and an automated test proves each rule against the ledgers (`EveryReportReconcilesToItsLedger`).

## Filter contract

- **From / To:** inclusive tenant business dates. The period can be at most 366 days, and From must not be after To. Point-in-time reports (stock on hand, inventory valuation, customer aging, supplier payables and cylinder custody) use To as the as-of date and ignore From.
- **Branch:** optional. Omitted means every branch the member can access; members without branch management see only their granted branches. Inactive branches stay reportable so history remains visible. A branch outside the member's access returns 404.
- **Type:** optional and report-specific (listed below). Unknown values return 400.
- **Pages:** 100 rows per page on screen. Totals always cover every row, not just the current page. A report above 100,000 rows returns 413; narrow the filters.

## Permissions

`reports.view` opens the Reports page and the dashboard. `reports.export` allows exports. Each report also requires the permissions of the data it shows, so a report never reveals more than the member could see elsewhere:

| Report               | Also requires                                        | Type filter                                            |
| -------------------- | ---------------------------------------------------- | ------------------------------------------------------ |
| Daily sales          | sales.view                                           | —                                                      |
| Purchases            | purchases.view_cost                                  | —                                                      |
| Stock on hand        | inventory.view                                       | Lpg, Cylinder, SparePart                               |
| Stock movement       | inventory.view                                       | Lpg, Cylinder, SparePart                               |
| Inventory valuation  | inventory.view                                       | Lpg, Cylinder, SparePart                               |
| LPG profit           | sales.view_profit                                    | —                                                      |
| Spare-parts profit   | sales.view_profit                                    | —                                                      |
| Expenses             | finance.view                                         | Operating, Capital                                     |
| Gross and net profit | sales.view_profit, finance.view                      | —                                                      |
| Customer aging       | finance.view                                         | —                                                      |
| Supplier payables    | finance.view, purchases.view_cost                    | —                                                      |
| Cash reconciliation  | finance.view                                         | Cash, Bank                                             |
| Cylinder custody     | sales.view                                           | —                                                      |
| Branch comparison    | sales.view_profit, finance.view, purchases.view_cost | —                                                      |
| User activity        | audit.view                                           | access, inventory, purchasing, sales, finance, reports |

The migration grants both reporting permissions to owner roles only.

## Definitions and reconciliation

- **Daily sales:** sale-journal activity per business date and branch: invoice count, invoiced and reversed revenue, net revenue, cash received, advances applied and receivable change. Net revenue equals the sum of sale-journal revenue; receivable change equals the sum of receivable movements.
- **Purchases:** supplier invoices per date, branch and supplier, with reversals, cash paid to the supplier, separately paid purchase costs and advances applied. Net purchases equal invoice plus reversal payable.
- **Stock on hand:** quantity and value per warehouse, item and stock state at the end of the as-of date. Stock is dated by its document's business date. Total value equals the stock-movement values up to that date.
- **Stock movement:** opening, received, issued and closing quantity and value per warehouse and item. Movements are netted within each document first, so a state change inside one document (for example Full to CustomerHeld) is not counted as both a receipt and an issue. For every row, opening + received − issued = closing; closing value equals stock on hand.
- **Inventory valuation:** weighted-average value per item across the selected warehouses, including business-owned cylinders held by customers. Average cost = value ÷ quantity, to 6 decimal places. The total equals stock on hand.
- **LPG profit and spare-parts profit:** revenue, cost of goods sold, gross profit and margin per date and branch. Spare-part revenue comes from spare-part lines in the frozen sale snapshot, and spare-part cost from that sale's stock movements. LPG is the journal amount minus spare parts, so the two reports always add up exactly to sale-journal revenue and cost. LPG cost includes recorded filling loss.
- **Expenses:** expenses net of reversals per category and branch, classified as operating or capital. The total equals the cash paid by expense entries and their reversals.
- **Gross and net profit:** a statement of LPG and cylinder sales, spare-part sales, cylinder usage and damage charges (cylinder-journal revenue), cost of goods sold by category, gross profit, operating expenses and net profit. Capital expenses appear as a memo line and are not deducted.
- **Customer aging and supplier payables:** outstanding invoices, plus cylinder settlements for customers, at the as-of date, aged from the invoice business date into Current, 1–30, 31–60, 61–90 and 90+ days. Also shows unapplied advances and net balance. Totals equal journal receivables or payables up to the as-of date. A customer's net balance matches the Udhaar view.
- **Cash reconciliation:** one row per account and date with activity or a register session. Columns: opening balance, every line of the cash formula, expected close, counted cash, variance and register status. The formula lines are sales and collections, customer advances, cylinder deposits/fees/refunds, purchases and purchase costs, supplier advances, expenses, loans and owner funds, transfers and bank deposits, and adjustments. Opening plus the lines equals expected close, which matches the cash register. This closes the per-category breakdown carried over from Phase 07.
- **Cylinder custody:** per customer and cylinder type: cylinders on loan, refundable deposits held and settlement amounts due, as of the date. Cylinders on loan equal customer-held stock in the inventory ledger.
- **Branch comparison:** net revenue, cost, cylinder charges, gross profit, net purchases, operating expenses, net profit and sale cash received per branch. Rows sum to the profit statement, purchases and expenses reports.
- **User activity:** audited actions per user and action, with first and last times in the tenant time zone. It is workspace-wide, so the branch filter does not apply. Only current or former members are named. The count equals audit events in the period.

## Dashboard

The overview page shows the dashboard to members with `reports.view`, for the last 7, 30 or 90 days, across all accessible branches or the selected branch. Each figure appears only with its permission:

- **Always shown:** net revenue (sales.view).
- **With sales.view_profit:** gross profit, including cylinder charges.
- **With finance.view:** operating expenses, current cash and bank balance, and customer receivables including cylinder settlements; supplier payables also need purchases.view_cost.
- **With inventory.view:** the number of active items at or below their reorder level in sellable stock.

The charts are a daily trend line (revenue, gross profit and operating expenses) and ranked bars for expenses by category and revenue by branch. They are drawn with Recharts from server totals. Their colors are the validated categorical palette; the low-contrast series also has a dashed line, a legend and a table view.

## Export lifecycle

1. `POST /api/workspace/reports/exports` validates the report, permissions, filters and branch access immediately. It then queues a job and records `report.export_requested`. The command key makes retries safe; reusing a key for a different export returns 409. A member can have at most five exports queued or running.
2. A background worker in the API claims jobs with `FOR UPDATE SKIP LOCKED`, so several API instances never process the same job. It rebuilds the requester's tenant scope and live permissions from the database, so a membership or permission revoked after queueing stops the export. It renders CSV (UTF-8 with BOM, formula-injection-safe cells), XLSX (numeric cells with number formats, plus an About sheet recording definition and filters) or PDF (landscape, paginated, at most 5,000 rows). It then records `report.export_completed`.
3. Transient failures retry up to three attempts, and a five-minute lease recovers jobs from a crashed worker. Validation, permission and size problems fail at once with a readable reason.
4. Only the requesting member can download, and only while they still hold the report's permissions. Each download is audited as `report.export_downloaded`. Files expire 24 hours after completion (410 Gone); the worker then removes the stored file and keeps the job row as the audit trail.

Files are stored in PostgreSQL for the local deployment. Private object storage with signed URLs is a production-hardening item for Phase 11.

## Migration

Apply `ReportingAndExports` before starting the updated API. It is additive: it creates the export-job table and business-date indexes on the sale, purchase, finance, cylinder and stock-document journals, and grants the two reporting permissions to owners. No posted history changes.
