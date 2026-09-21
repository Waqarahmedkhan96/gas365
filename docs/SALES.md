# Customers, prices and sales drafts

Phase 06 is in progress. This checkpoint provides customer records, price lists and editable sales drafts. It does not provide posting, receipts, sales payment capture, cylinder custody, exchanges or deposits. Those remain required Phase 06 work; a priced cylinder draft does not perform any cylinder movement.

## Customers and permissions

Customers are tenant-wide contact records with name, phone, address and active status. Search and directories use server pagination. Edits require the last returned Version. Deactivate rather than delete; all material edits are audited without copying personal details into the audit event.

`customers.view` permits customer and selling-price reads. `customers.manage` permits customer edits, and `prices.manage` permits price-list/price edits; both also require customer view. `sales.view` permits branch-scoped draft reads, `sales.create` permits draft edits, and `sales.discount` permits any nonzero line discount. Sales users need customer view for the selection screens. Price managers and sales-draft editors can read catalog metadata but do not gain inventory balances, cost ledgers or stock-posting permissions.

Every endpoint uses the existing server session, CSRF protection and workspace header. Tenant query filters, guarded writes and tenant-qualified foreign keys prevent cross-tenant references. Draft operations also authorize the original and requested branches. Sales draft edits do not permit a user to move a draft out of an inaccessible branch.

## Selling prices

Named price lists contain one entry per item and sale mode. LPG supports Refill; cylinders support FullCylinder, CylinderOnly, Exchange and LoanRefill; spare parts support SparePart. Mode compatibility is validated on the server. This catalog configuration does not approve unresolved exchange, gas-conversion or deposit policies.

An authorized price manager supplies a PKR unit price with up to six decimal places, matching the existing price/cost precision. The safety bound is PKR 1,000,000 per unit, not a commercial price limit. A price is per catalog unit for LPG/parts and per whole cylinder for cylinder modes. LoanRefill identifies the selling price of the gas service only; deposits are separate and not implemented here. No tax rate or deposit amount is invented or seeded.

Price identity (item/mode) is immutable after creation; deactivate it and add a different price to change identity. Prices and lists use optimistic concurrency, and duplicate item/mode prices within a list are rejected. Inactive lists cannot accept price edits. Price changes do not silently refresh saved draft snapshots.

## Sales drafts

A draft has one branch, customer and price list, reference, optional notes and 1–100 lines. Each line references a saved price and its version, a warehouse within the selected branch, quantity and an absolute PKR line discount. The browser never submits authoritative unit prices, gross or net totals. The server reads saved prices, checks active references and quantity precision, calculates each line gross with midpoint-away-from-zero rounding to two PKR places, validates discount and sums rounded line amounts. Nonzero discounts require separate permission. Negative quantities, over-discounting and stale price versions are rejected.

Saving uses a consistent database snapshot and optimistic draft version. Concurrent edits produce one successful write and a conflict rather than lost changes. A draft records the server-calculated customer/price/item/warehouse description snapshot. Later price edits do not mutate it. Re-saving requires the current price versions, so the operator must review changed rates. No stock is reserved, no COGS or profit is claimed, and no customer receivable, cash entry or deposit liability is recorded. Posting must later revalidate all relevant state and provide immutable, atomic/idempotent effects.

A draft is not a receipt or completed sale. Customer sales history, posting/correction history, PDF receipts, full-cylinder gas component handling, empty/full exchange valuation, borrowed-cylinder custody, deposits and separately paid purchase costs remain incomplete.

## API and screens

Routes are under `/api/workspace/sales`: `customers`, `price-lists`, `price-lists/{id}/prices` and `drafts`. POST creates configuration or a draft; PUT updates with Version; GET reads paginated directories/details. No hard-delete or financial-posting route is introduced. Screens are `/workspace/customers`, `/workspace/prices` and `/workspace/sales`.

## Migrations and historical data

Apply `20260921002027_CustomersAndPriceLists` and `20260921070611_SalesDrafts` before the API. They add Customers, PriceLists, SalePrices and SaleDrafts, tenant-qualified constraints/indexes and owner permissions. Other roles need explicit grants. No business records are seeded, existing purchases remain unchanged, and inventory is not recosted. Downgrade drops these new records and is not a recovery procedure for valuable data. Both migrations form an incomplete Phase 06 checkpoint; do not describe them as full sales support.

## Approved cylinder deposit and usage policy

The owner accepted the recommendations in this task and clarified that workers enter the usage charge manually on return. It may differ each time; PKR 2,000 was an example, not a fixed or monthly rate. No automatic duration-based charge is authorized.

- Configure a suggested deposit per cylinder type, but permit manual non-negative deposit amounts, including zero, subject to permissions. Reductions/waivers require authorized staff; admin/store-manager-approved zero-deposit customer arrangements support hotels and tandoors.
- Borrowed cylinders remain business-owned and are tracked against the customer even when no deposit is collected. Return moves the cylinder to empty stock or inspection according to condition, without treating the shell as a new purchase.
- A worker with return/usage-entry permission can enter the variable usage fee when processing the return. Do not require manager entry or approval of an ordinary usage fee merely because damage/loss charges need approval. Record actor, amount and return reference; show fee terms to the customer when issuing the cylinder.
- Damage or loss charges require admin/store-manager approval; ordinary workers cannot approve them. Permission is checked by the API, not inferred from a client role label.
- Deposit collection increases cash and refundable deposit liability, not sales income. The earned usage fee is separate income; applying it against the deposit reduces the liability. Refundable deposits are shown in cash received, separate from revenue/profit.
- Return settlement displays original deposit, usage fee, approved damage/loss charges and refund due. Only actual payment records a cash refund; an unpaid refund remains owed. Approved charges beyond a deposit, including a zero-deposit loan, must be collected separately rather than producing a negative refund.
- All cash, fee, custody and refund effects need atomic/idempotent posting and auditable reversal. This is an approved policy, not a claim that the workflows are implemented.
