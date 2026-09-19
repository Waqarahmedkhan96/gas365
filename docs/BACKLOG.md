# Prioritized backlog

Priority P0 blocks a safe foundation or financial workflow; P1 completes the functional MVP; P2 follows MVP. Phase order remains mandatory regardless of priority. Requirement IDs refer to the [traceability matrix](REQUIREMENTS.md).

| Order   | Priority  | Phase / requirements | Deliverable and exit gate                                                                                            | Dependencies                            |
| ------- | --------- | -------------------- | -------------------------------------------------------------------------------------------------------------------- | --------------------------------------- |
| 1       | P0        | 01 / R01,R19         | Pin supported runtimes; scaffold layers, web and tests; PostgreSQL Compose, health/OpenAPI, CI and build checks pass | Phase 00                                |
| 2       | P0        | 02 / R02,R18         | Identity, tenants, branches, permissions, sessions and basic limits; cross-tenant negatives pass                     | Hosting/auth decisions; 01              |
| 3       | P1        | 03 / R03             | Responsive application shell, tokens, table/form primitives; accessibility/component checks                          | 02                                      |
| 4       | P0        | 04 / R04–R06,R15     | Catalog, custody, immutable movements and weighted cost; stock and concurrency invariants pass                       | Cost/rounding/cylinder decisions; 02–03 |
| 5       | P0        | 05 / R07,R14,R15     | Suppliers, purchase posting, landed cost and voucher; atomic payable/stock reconciliation                            | 04                                      |
| 6       | P0        | 06 / R08,R09,R14,R15 | Sales modes, customers, exchanges and receipts; COGS/stock/payment E2E checks                                        | Pricing/tax/return decisions; 05        |
| 7       | P0        | 07 / R09–R12         | Allocations, aging, expenses, actual loans and cash close; ledger/cash reconciliation                                | Closed-period and advance rules; 06     |
| 8       | P1        | 08 / R13,R14         | Dashboard and all required reports/exports; filters, permissions and reconciliation pass                             | 04–07                                   |
| 9       | P1        | 09 / R15–R17         | Audit explorer, safe imports, notifications and retries; import preview/idempotency checks                           | Import/cutover policy; 08               |
| 10      | P1        | 10 / R18             | Subscription plans, metering, onboarding and support access                                                          | Provider selection; 09                  |
| 11      | P0 launch | 11 / R19             | Staging, scans, restore drill, load/a11y evidence and approved production workflow                                   | Recovery objectives; 10                 |
| 12      | P2        | 12 / R20             | Controlled rollout, feedback and opt-in deterministic reorder suggestion                                             | Launch gates; 11                        |
| Pending | Unranked  | Unassigned / R21     | Delivery dispatch/assignment/proof-of-delivery scope proposal                                                        | Product owner decision                  |

Each phase delivers tests and documentation with its vertical slice. Audit, reversal, tenant isolation and export authorization begin with the first affected module; later explorer and hardening phases do not postpone those controls.

Phases 00–04 are complete; the owner approved Phase 04 costing and final acceptance on 2026-09-19, recorded in PROJECT_STATE.md. Do not begin Phase 05 automatically.
