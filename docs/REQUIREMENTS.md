# Requirement traceability

Sources: [business rules](../reference/BUSINESS_RULES.md), [architecture/security](../reference/ARCHITECTURE_AND_SECURITY.md), [UI](../reference/UI_SYSTEM.md), [operations/reporting](../reference/DEVOPS_TESTING_REPORTING_AI.md), [product blueprint](../reference/PRODUCT_BLUEPRINT.md), and [inspected source inventory](SOURCE_INVENTORY.md). W denotes workbook sheet; S denotes screenshot range from that inventory. The table defines acceptance targets; completed evidence for R01 and R02 is in the [Phase 01](PHASE_01_REPORT.md) and [Phase 02](PHASE_02_REPORT.md) reports. R18 currently includes server-enforced user/admin/branch limits only; billing and support administration remain Phase 10.

| ID  | Requirement and evidence                                                                  | Phase      | Acceptance evidence                                                              |
| --- | ----------------------------------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------- |
| R01 | Reproducible web/API/PostgreSQL platform; architecture specification                      | 01         | Pinned tools, build/test CI and healthy Compose startup                          |
| R02 | Identity, memberships, branches, permissions; S1–16, S139–142                             | 02         | Revoked sessions fail; tenant A cannot read/write/export tenant B data           |
| R03 | Accessible responsive navigation/forms/tables; S1–121, UI specification                   | 03         | Keyboard flows, measured AA contrast, mobile and denied/error states             |
| R04 | Separate LPG, cylinders and spare parts; W Inventory/Lists; S17–35                        | 04         | Unit/quantity precision, ownership and configurable serial validation            |
| R05 | Immutable stock ledger, transfers, adjustments; W Inventory/Stock Adjustments             | 04         | Stock equals posted movements; concurrent issues cannot oversell by default      |
| R06 | Weighted-average costing and snapshots; W _SP_FIFO/LPG Profit conflict                    | 04         | Decimal examples, concurrency and stored COGS tests; ADR 004                     |
| R07 | Suppliers, purchases and landed cost; W LPG Purchases/Spare Parts Purchase; S36–49,95–102 | 05         | Atomic stock/payable/payment posting, allocation totals and voucher              |
| R08 | Multi-line sales and cylinder exchanges; W Sep 01–30; S50–58,80–84                        | 06         | Server totals, stock/custody effects, discount permissions, receipt              |
| R09 | Customer history and partial payments; W Sales Overview/LOANS; S59–67,78–87               | 06–07      | Original sale preserved; later allocations cannot overpay without advance policy |
| R10 | Append-only receivable/payable ledgers and aging; W Loan Overview; S73–87                 | 07         | Ledger balances and allocation sums reconcile; unapplied credit visible          |
| R11 | Expenses, borrowing and owner funds; W Major Expenses; S68–77                             | 07         | Inventory purchases and loan principal excluded from operating expenses          |
| R12 | Cash opening, closing and controlled reopening; W daily sheets; S88–94,114–115            | 07         | Expected/count/variance retained; closed-day changes require authorization       |
| R13 | Report definitions, filters and dashboard; W overviews/weekly/monthly; S103–115           | 08         | All report totals reconcile to ledger snapshots under tenant/branch filters      |
| R14 | PDF vouchers/receipts and XLSX/CSV exports; S41–43,53–54                                  | 05–08      | Permission checks, audit entries and private expiring downloads                  |
| R15 | Reversal and audit trail; security/business specification                                 | 04–09      | Original immutable; reversal references original and balances net correctly      |
| R16 | Validated import; workbook evidence and operations specification                          | 09         | Preview/error file, idempotent commit and no direct ledger writes                |
| R17 | Durable notifications/jobs; operations specification                                      | 09         | Tenant-bound payloads, retries and duplicate-delivery protection                 |
| R18 | Plans, onboarding and support access; S116–121,140–142                                    | 02,10      | Server limits; isolated, time-bounded, audited support access                    |
| R19 | Security, migrations, backup and restore; architecture/operations specification           | 01–11      | Empty/upgrade migrations, scans, isolated restore and staging smoke evidence     |
| R20 | Pilot and advisory automation; blueprint/operations specification                         | 12         | Controlled onboarding, data-quality baseline and measurable reorder suggestion   |
| R21 | Delivery workflow; blueprint only, no dedicated source flow                               | Unassigned | Owner-approved dispatch/custody scope before scheduling                          |

## Required reporting coverage

R13 includes daily sales, purchases, stock on hand, stock movement, inventory valuation, LPG profit, spare-parts profit, expenses, gross/net profit, customer aging, supplier payable, cash reconciliation, cylinder custody, branch comparison and user activity. Each report needs date/time-zone and reversal semantics, permission and filter contracts, and reconciliation fixtures. Charts use server aggregates and have a table or text alternative.

## Cross-cutting invariants

Tenant context comes from authenticated membership. Every branch selection is checked against access. Money, weights and costs use explicit decimal precision. Posting is atomic and idempotent; conflicting reuse of an idempotency key must fail. Inventory, payment allocation and cash-close races require concurrency coverage. Posted snapshots are preserved. Exports and jobs obey the same tenant restrictions as interactive requests.
