# Credit, expenses, loans and cash registers

Phase 07 adds branch money accounts, customer and supplier advances with allocations, invoice aging, expenses, owner funds, principal-only loans and controlled cash-register sessions. PostgreSQL owns every balance. Invoice journals from Phases 05 and 06 remain the authoritative receivable and payable records; finance entries add money movements and allocations without duplicating them.

## Accounts and opening balances

Each branch has Cash and Bank accounts. The first Cash account becomes the branch's default cash drawer. An account opens with an operator-reconciled opening balance and a reason. No earlier cash is copied or assumed to be in the drawer. Creating the account requires finance.manage; accounts and expense categories are append-only and cannot be renamed or deleted.

Creating the default cash drawer activates drawer controls for that branch. From then on, cash from sale payments, purchase payments, separately paid purchase costs and cylinder deposits or refunds is recorded in the drawer automatically, in the same transaction as the original document. Such cash is rejected while that day's register is not open, or after it is closed. The API returns the guard's message and tells the user to ask an authorized person to open or reopen the register. Branches without a cash account keep the Phase 06 behavior.

## Finance entries

Every entry is immutable, branch-owned and idempotent. The command key is at most 80 characters, and the internal: prefix is reserved. Reusing a key with a different payload returns 409. Amounts are positive decimals with two places. The server signs the cash effect.

| Kind                         | Cash effect                   | Notes                                                                                                                   |
| ---------------------------- | ----------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| CustomerReceipt              | In                            | Customer advance. It stays unapplied until allocated.                                                                   |
| SupplierPayment              | Out                           | Supplier advance. It stays unapplied until allocated.                                                                   |
| Allocate                     | None                          | Applies unapplied money to one invoice for the same party. It adds an Allocation entry to the sale or purchase journal. |
| Expense                      | Out                           | Requires an expense category. Capital categories are flagged separately from operating expenses.                        |
| OwnerFunds / OwnerWithdrawal | In / Out                      | Owner investment and withdrawal.                                                                                        |
| LoanReceived / LoanGiven     | In / Out                      | Principal only, with a named counterparty. No interest or due-date terms.                                               |
| LoanRepayment                | Opposite of the loan          | Cannot exceed the outstanding principal.                                                                                |
| Transfer                     | Out, with a paired TransferIn | Moves money between two accounts in the same branch, for example a bank deposit.                                        |
| CashAdjustmentIn / Out       | In / Out                      | Reasoned variance correction. Requires finance.manage.                                                                  |

Allocation cannot exceed the payment's unapplied amount or the invoice's outstanding balance. It is rejected for reversed invoices. Allocations currently apply to sale and purchase invoices only. Cylinder settlement receivables appear in aging but cannot yet be settled from an advance.

## Reversals

Reversals require finance.reverse and a reason. They record exact opposite entries on the current business date. Reverse allocations and repayments before their parent payment or loan. Reverse sale or purchase allocations before reversing the invoice itself. Opening entries, automatically captured document cash and paired TransferIn entries are corrected through their originating document, not directly.

## Cash register

One session history exists per cash account and business date:

- Open records the counted cash. Earlier sessions must be closed, and dates must open in order.
- Close records counted cash, expected cash and the variance with a reason. It requires register.close.
- Reopen requires register.reopen and is blocked once a later date has been opened. Correct older days with a current-date adjustment instead.

Expected close equals opening plus every signed cash movement for that date. That covers each line of the business-rules formula: cash sales, collections, loans and owner funds received, other cash in, cash purchases, expenses, supplier payments, loan repayments or owner withdrawals, and bank deposits as transfers. A recorded variance never changes book cash. An authorized user must post a separate reasoned CashAdjustment. Register actions use a command key and the last event ID for optimistic concurrency.

## Credit and aging

The Udhaar view shows each party's balance, unapplied amount, open invoices and ledger lines. Aging uses the invoice business date in the buckets Current, 1–30, 31–60, 61–90 and 90+ days. The customer balance is debits minus credits; the supplier balance is credits minus debits.

## Permissions and isolation

The permissions are finance.view, finance.post, finance.manage, finance.reverse, register.close and register.reopen. The migration grants them to owner roles only; team roles need explicit assignment. Every command rechecks the tenant, branch access and the account's branch. Foreign tenant or branch references return 404. Database triggers make finance and register rows append-only and enforce drawer controls even for direct inserts.

## Migration

Apply `20260928120055_FinanceAndRegisters` before starting the updated API. It is additive: it creates four tables, append-only and cash-guard triggers, capture triggers on the sale, purchase and cylinder journals, and owner permissions. No finance rows are created and no posted history changes. Downgrading drops the finance tables and is destructive; use backup and restore for recovery.
