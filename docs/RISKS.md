# Risks and open decisions

Owners below are project roles, not assigned individuals. No legal or financial compliance determination was performed in Phase 00.

| ID | Severity | Risk / decision | Owner | Required action and gate |
| --- | --- | --- | --- | --- |
| K01 | Critical | Cross-tenant data exposure | Engineering | Filters, command checks, composite constraints and negative tests from Phase 02; exports/jobs included |
| K02 | Critical | Partial or duplicate financial posting | Engineering | Atomic transactions, scoped idempotency and concurrency tests before each posting module |
| K03 | High | FIFO history differs from weighted-average results | Product owner/accountant | Agree opening quantity/value and cutover reconciliation before importing or Phase 04 acceptance |
| K04 | High | Undefined cost bucket, rounding, returns and backdating | Product owner/accountant | Define precision/scales, residual allocation and valuation policy before Phase 04 |
| K05 | High | Pakistan invoice/tax/FBR requirements unresolved | Business owner with qualified adviser | Determine applicable requirements before production invoice design is finalized |
| K06 | High | Cookie design depends on hosting topology | Engineering | Confirm same-origin routing, session lifetimes and key/revocation storage before Phase 02 |
| K07 | High | Cylinder deposits, ownership and serial obligations unclear | Operations owner | Confirm item-level serial policy, custody/deposit rules before Phase 04/06 |
| K08 | High | Workbook loan labels conflate receivables and borrowing | Accountant | Separate migration categories; approve settlement/advance rules before Phase 07/import |
| K09 | High | Closed-period corrections or consumed stock reversal corrupt history | Accountant/engineering | Define partial reversal and dependency policy before affected posting workflow |
| K10 | High | Backup exists but recovery fails | Operations | Select RPO/RTO, retention and providers; record isolated restore evidence before real-data release |
| K11 | Medium | Delivery scope lacks implementation phase | Product owner | Approve explicit scope and phase before scheduling delivery work |
| K12 | Medium | Subscription provider and support access undecided | Product owner/engineering | Select provider and audited support workflow before Phase 10 |
| K13 | Medium | Copied assets or committed business records | Engineering | Exclude source binaries/pack; use original UI wording/assets and synthetic test data |
| K14 | Medium | Source inventory inaccuracies | Engineering | Record actual 142 PNGs, 47 sheets and missing VBA companion; do not invent missing input |
| K15 | Medium | Product name/domain/legal entity unconfirmed | Business owner | Complete business checks before commercial launch |
| K16 | High | Phase 08 mistaken for production approval | Product owner/operations | Apply release gates in scope document and require explicit production authorization |

Unresolved items are not permission to invent rules. Record approved decisions in the relevant ADR and project state when implementation reaches their gate.
