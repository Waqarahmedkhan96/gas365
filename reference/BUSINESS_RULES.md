# Gas365 Core Business Rules

## Numeric policy

- Money: decimal, currency PKR initially, 2 fractional digits.
- LPG quantity/weight: decimal with configurable precision; never binary floating point.
- Exchange rates are out of MVP scope unless multi-currency is later approved.
- Store authoritative values and rounding policy on posted document lines.

## Products and stock

Model LPG gas, cylinder assets and spare parts distinctly while sharing catalogue concepts where useful.

An LPG configuration includes brand, domestic/commercial classification, nominal capacity, allowed sale modes and active price schedule. Cylinder tracking can be quantity-based or serial-based per item. Spare parts include category, SKU, unit, reorder level and selling price.

Stock is a ledger of immutable movements. Current stock is the sum of posted movements, not an editable balance. Movement types include purchase receipt, sale issue, exchange receipt/issue, transfer out/in, customer return, supplier return, damage, leakage, adjustment, opening balance and reversal.

No movement may make a controlled stock bucket negative unless the tenant explicitly enables and is authorized for negative stock; default is prohibited.

## Costing

Initial costing is perpetual weighted average:

```text
new average cost = (old quantity * old average cost + received quantity * receipt unit cost) / new quantity
COGS = issued quantity * current average cost at posting time
```

Posted COGS is stored as an immutable snapshot. Landed cost can allocate transport/loading charges by weight, quantity or line value with a visible allocation rule. Keep costing behind an interface so FIFO can be added later without rewriting sales.

## Purchases

Draft -> Approved/Posted -> Partially Paid/Paid -> Reversed.

Posting a purchase atomically creates stock receipts, inventory value/cost updates, supplier payable or cash/bank payment entries, expense allocations where applicable and audit entries. Drafts can change; posted purchases require reversal/correction.

## Sales

Sale modes include gas only/refill, filled cylinder, cylinder only, empty-for-filled exchange and spare part. A single sale may contain multiple lines.

Server calculates line gross, discount, net, taxes if configured, paid, outstanding, COGS and gross profit. Payments never exceed payable amount without an explicit customer advance workflow.

Posting atomically creates stock issues/exchange movements, customer receivable or cash/bank receipt, revenue, COGS, document snapshot and audit entry.

## Cylinders

Track full, empty, in transit, customer-held, under inspection, damaged, lost and retired states. Serial-tracked cylinders also keep owner, brand, capacity, tare weight, inspection information, location and custody history. A serial cannot be in two locations or states simultaneously.

Deposits and cylinder balances are distinct from LPG revenue. Customer-held cylinder quantity must reconcile to custody movements.

## Credit and payments

Do not store only a mutable current balance. Use an append-only customer/supplier subledger containing invoice, payment, return, discount, opening balance, adjustment and reversal entries. Allocation tables map payments to invoices; unapplied credit remains visible.

```text
customer balance = debits - credits
supplier balance = credits - debits
```

## Cash register

Each branch/business date supports one controlled register session per cash drawer:

```text
expected close = opening cash
               + cash sales
               + credit collections
               + loans/owner funds received
               + other cash in
               - cash purchases
               - expenses
               - supplier payments
               - loan repayment/owner withdrawal
               - bank deposits
```

Closing records counted cash, expected cash, variance, reason and approver. Closed periods require controlled reopening or adjustment; never silently recalculate history without a trace.

## Expenses and loans

Purchases of inventory are not operating expenses. Expense categories are configurable and distinguish shop, payroll, delivery/fuel, utilities, maintenance and major/capital items. Loans record counterparty, principal, disbursement, repayments, outstanding balance and optional interest schedule. Owner investment/withdrawal is not revenue/expense.

## Profit

```text
net sales = gross sales - discounts - returns
gross profit = net sales - COGS
operating profit = gross profit - operating expenses
```

Cash received is not profit. Purchase payments are not COGS until stock is sold. Credit sales affect revenue/profit when posted while cash arrives later. Stock adjustments, leakage and damage affect inventory and profit through documented rules.

## Corrections

Drafts can be edited/deleted according to permission. Posted records are voided/reversed with reason, user and timestamp, then optionally replaced. Reports include or clearly exclude reversed records consistently.
