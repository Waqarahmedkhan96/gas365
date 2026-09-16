# Product Blueprint and Delivery Stages

## Product position

Gas365 is an LPG operations and profitability platform for retailers and distributors. It combines LPG, cylinders, spare parts, purchases, sales, customer credit, supplier payables, expenses, cash, delivery and audit records in one tenant-isolated system.

## Delivery model: 13 gated stages (00–12)

| Stage | Deliverable                                                  | Exit condition                                                                  |
| ----- | ------------------------------------------------------------ | ------------------------------------------------------------------------------- |
| 0     | Source inventory, ADRs, backlog and repo conventions         | Requirements traceability and decisions recorded                                |
| 1     | Monorepo skeleton and local platform                         | Web/API/Postgres start with one command; CI baseline green                      |
| 2     | Identity, tenants, branches, roles and permissions           | Cross-tenant tests prove isolation                                              |
| 3     | Design system and application shell                          | Responsive, accessible shell and reusable tables/forms                          |
| 4     | Catalog, LPG types, cylinders, spare parts and stock ledger  | Stock derives from movements and reconciles                                     |
| 5     | Suppliers and purchasing                                     | Posted purchase creates payable/cash and stock/cost effects atomically          |
| 6     | Customers, sales, exchanges and receipts                     | Posted sale creates revenue, COGS, stock and receivable/cash effects atomically |
| 7     | Credit, supplier payments, expenses, loans and cash register | Subledgers and daily cash reconcile                                             |
| 8     | Reports, PDF/Excel exports and dashboard                     | Reports tie to ledgers and respect permissions                                  |
| 9     | Audit, reversals, imports and notifications                  | Corrections are traceable; Excel import has preview/error workflow              |
| 10    | Subscription limits, onboarding and platform administration  | Plan limits enforced server-side; support access audited                        |
| 11    | Hardening, staging, backup/restore and observability         | Security, load, restore and deployment gates pass                               |
| 12    | Pilot release and post-pilot automation/AI                   | Controlled tenant onboarding and measured feedback                              |

## MVP boundary

Stages 0–8 form the sellable pilot MVP. Stages 9–11 are mandatory before broad commercial launch. Stage 12 contains improvements driven by evidence.

## Primary roles

- Platform Super Admin
- Tenant Owner
- Tenant Admin
- Branch Manager
- Cashier/Sales Worker
- Inventory/Purchase Worker
- Accountant
- Delivery Driver
- Read-only Auditor

Roles are bundles of permissions, not hard-coded authorization paths.

## Initial subscription controls

Plan limits may include branches, admins, workers, monthly transactions, storage and advanced reports. Limits are enforced in backend commands and may never rely on hiding frontend buttons.

## Out of scope until evidence justifies it

- Microservices and Kubernetes
- General ledger comparable to a full accounting suite
- Native mobile applications
- Full offline transaction synchronization
- Redis
- AI auto-posting of financial transactions
- Fully autonomous pricing decisions
