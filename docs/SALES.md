# Sales, cylinder custody and receipts

Phase 06 implements customers, price lists, multi-line drafts, posting, payment capture, receipts, exchanges and cylinder loans. PostgreSQL owns all totals, stock and money. The previous draft-only checkpoint remains documented in the phase report as historical evidence.

## Prices and drafts

Customer contacts and price lists are tenant-wide, searchable and paginated. Configuration edits require the last returned Version. Prices are per catalog unit and support up to six decimal places. LPG supports Refill; cylinders support FullCylinder, CylinderOnly, Exchange and LoanRefill; spare parts support SparePart. A price identity is immutable; deactivate it to replace the item/mode.

Drafts contain 1–100 lines in one branch, customer and price list. The server reads price versions and calculates rounded gross, absolute discount and net. Nonzero discounts require sales.discount. Quantities follow the catalog unit precision, with whole cylinders and up to three decimal places for LPG. Drafts have no financial or stock effect. Posting rechecks active references and saved price/policy versions. A posted draft cannot be edited, including through direct SQL.

## Filling and cylinder stock

Use a KG LPG item for kilogram sales: 100 g = 0.1 kg, 1,600 g = 1.6 kg and 3,500 g = 3.5 kg. Each customer cylinder filled gets its own line. Optional customer-cylinder and source-cylinder references provide traceability. A manual nonnegative loss uses the same unit as the sold quantity and requires a reason when positive. Loss is visibly labelled in red. Stock consumption is sold quantity plus loss; only sold quantity is charged. Immutable COGS includes consumed LPG, including filling loss.

References are operator-entered identifiers; they do not create a separate gas balance per physical source cylinder. The approved cost/quantity pool remains tenant, item and warehouse. Source-cylinder capacity reconciliation or automatic weighing is not implemented.

Configure each cylinder type with an LPG item using KG, kilograms per filled cylinder, suggested deposit and usage terms. Shell stock/value must exclude gas recorded separately as LPG stock. This is explicit in the configuration screen; no historical stock is automatically split or recosted. Configuration requires cylinders.configure and version checks.

- Refill consumes the entered LPG quantity and loss from Available stock.
- CylinderOnly sells an Empty shell and snapshots its issue cost.
- FullCylinder sells a Full shell plus its configured LPG quantity.
- Exchange receives a matching empty cylinder and gives a Full cylinder. Shell quantity/value is preserved; configured LPG is consumed once. Tracked incoming/outgoing serials must differ, belong to that cylinder type and be in valid states.
- LoanRefill sells the configured LPG and transfers the business-owned shell from Full to CustomerHeld. Each shell has a separate immutable customer loan record, including serial when tracked, deposit and terms. Partial returns are handled by selecting individual loans.

Negative stock and invalid serial locations are rejected. Multi-line movements are serialized by the tenant lock and committed atomically with financial entries. Failure on a later line rolls back all earlier effects.

## Posting, cash and corrections

Sales post an immutable invoice, stock document, COGS snapshot and optional cash payment in one transaction. Cash can be partial; the remaining amount is a customer receivable. Later cash allocations require the last financial entry ID and cannot exceed the balance. Advances, bank accounts and cash closing remain Phase 07.

Every financial command requires an idempotency key of at most 80 characters. Retry an uncertain response with the same key and identical payload. Conflicting reuse returns 409. The internal: prefix is reserved. UI forms retain a command key for an unchanged request during the current page session. After reload, inspect history before submitting again.

Payment reversals require a reason and record an actual cash correction/return; they do not send money. Reverse sale payments separately before reversing a sale. Full sale reversal adds exact opposite stock, revenue, receivable and COGS entries. Later affected stock/serial activity blocks reversal. A loan sale can be reversed only before return/settlement history exists; the command also returns its shell and records its deposit cash handed back, requiring payment permission. The screen explains this cash effect. Original document totals remain historical snapshots; signed ledger entries show their cancellation. Inventory's generic reversal cannot bypass financial corrections.

## Deposits, returns and approvals

The suggested deposit is editable manually at issue, including zero. Reductions below the suggestion require cylinders.waive_deposit, unless the customer has an approved zero-deposit arrangement. Such arrangements support hotels and tandoors and have a reason, approver, version and audit history. A positive issue deposit is recorded as cash actually collected and refundable liability, separate from the sale payment and revenue. At most 1,000 borrowed shells can be issued per sale.

A worker with cylinders.return enters a variable usage fee when processing each return. PKR 2,000 is an example, not a fixed or monthly rate. No manager approval is required for an ordinary usage fee. Damage/loss approval is a separate server-enforced cylinders.approve_damage_loss permission assigned to an administrator or store manager. The approval records the loan, condition, amount, reason and actor. A return can use only a matching approval; workers cannot submit arbitrary damage charges.

Acceptable returns move CustomerHeld stock to Empty or Inspection. Damaged and Lost outcomes require approval; Lost remains unavailable and is not a physical return to stock. Moving a shell to Lost preserves its existing cost pool value, consistent with the existing inventory state policy; it does not automatically write off its value.

Settlement earns usage plus approved damage/loss charges, applies at most the deposit, and separately exposes refund due or an additional receivable. For deposit 5,000 and usage 2,000, revenue is 2,000 and refund due is 3,000. Return itself creates no cash refund. A separate actual refund reduces cash and liability; partial refunds are allowed. Charges beyond the deposit are collected separately, never a negative refund. Zero-deposit custody is still tracked.

Refund/payment reversal preserves the original entry. A return can be reversed only after later cash effects have been reversed and before later affected stock activity. Direct editing/deleting of loans, financial entries or stock history is blocked. Generic stock commands cannot manipulate CustomerHeld stock for items managed by loans.

## Permissions and visibility

All routes require the server session, CSRF for writes, the current workspace and current permissions. Every tenant-owned table has a central filter and tenant-qualified references. Branch financial reads/writes authorize branch access. Customer history and loans are filtered by the selected branch.

customers.view/manage and prices.manage control profiles and prices. sales.view/create/discount/post/pay/reverse control their respective operations. sales.view_profit separately controls COGS and gross profit in responses. Cylinder configuration, deposit waiver, returns and damage/loss approval have separate permissions. Existing owners receive added permissions through migrations; other roles require explicit grants. Configuration metadata access does not grant stock-cost visibility.

## Screens, API and receipts

Sales provides drafts, posted register/detail, payments/reversals, cylinder policy setup and loan returns/refunds. Customers provides profiles, selected-branch sales/custody history and controlled zero-deposit arrangements. Price lists supplies server-owned selling rates.

Routes under /api/workspace/sales include customers, price-lists, drafts, posted, cylinder-policies and loans. Posted sale actions expose payments, payment reversal, full reversal and receipt. Loan actions expose approvals, return, refunds, payments and entry reversal. OpenAPI describes their contracts.

Receipts are authenticated, branch-authorized, generated in memory, audited and served no-store. They show original sale lines, cash/balance, payment/correction history and separate loan deposits/terms. Cost/profit is not printed on a customer receipt. English operational receipts are not tax invoices; Urdu shaping and tax/legal invoice configuration remain separate decisions.

## Migrations and operating boundaries

Apply CustomersAndPriceLists, SalesDrafts, PostedSales, SeparatePurchaseCosts and CylinderLoans before the API. Additive schema includes immutable-history triggers, constraints, indexes and owner grants. Prior stock is not recosted; prior purchases retain their original supplier-paid cost semantics. See purchasing contracts for separately paid costs on new documents. Downgrades destroy data; use backup/restore for recovery.

Current-date posting and full dependency-checked reversals retain the approved inventory policy. There is no automatic production deployment, source-cylinder gas subledger, automatic monthly usage fee, payment advance or cash-register closing in this phase.
