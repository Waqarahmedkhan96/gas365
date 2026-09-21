# Suppliers and purchasing

Phase 05 adds suppliers, purchase drafts, multi-line receipts, landed-cost allocation, cash payments, supplier ledgers, correction links and PDF vouchers. The API is under `/api/workspace/purchasing`, with the existing cookie, CSRF and `X-Workspace` boundary. PostgreSQL remains authoritative.

## Operating policy

The Phase 04 costing policy approved on 2026-09-19 remains unchanged: tenant/item/warehouse weighted-average pools across states, configured quantity precision up to 3 places, whole cylinders, 6-place unit costs, 2-place PKR values, midpoint away from zero and exact remaining-value consumption on final depletion. Purchase receipts contribute their exact allocated line values to these pools; rounded unit-cost snapshots are descriptive and are never multiplied back to recreate the ledger value.

Each purchase belongs to one branch and one supplier; all line warehouses must belong to that branch. Posting uses the tenant's current business date. Inputs contain quantities, negotiated supplier unit prices and received total line weights; the API calculates every line value, allocation and payable. Supplier unit prices are authorized inputs, not browser-calculated totals. A draft has no stock, payable or cash effect.

Transport/loading charges are capitalized into inventory. The visible allocation rule is Quantity, Weight or Value. Weight uses entered total received kilograms per line, not a guessed conversion from cylinder nominal capacity. A positive charge requires a positive basis; Weight requires positive weight on every line. Value uses each line's already-rounded base value. Allocation distributes whole cents using largest remainders, with line order breaking ties. This conserves the charge exactly without negative residual allocations. Zero-cost receipts are allowed; positive charges on zero-value lines require Quantity or Weight.

Phase 05 implementation choices, pending owner confirmation: charges belong to the purchase supplier's payable; separate freight-provider bills are deferred. Recorded payments must be individually reversed before full purchase reversal. These choices were presented during implementation and are not recorded as business approval. Payments are cash only, allocated to one purchase, with no advances or overpayments. Bank accounts, unapplied credits, aging and controlled cash-register closing remain Phase 07. Purchase cost is inventory, not an operating expense. Taxes and legal tax invoices are not configured in this phase.

## Posting and correction

Saving a draft validates its references and calculations and stores a display snapshot. Draft edits require its `Version`; after posting, both the original draft and posted snapshot are immutable. Posting revalidates the references and uses a separate immutable purchase document, an inventory receipt document, stock movements, a payable invoice entry, optional cash payment entry and audit events. A single database transaction commits them together under the existing tenant stock lock.

Purchase and payment commands require an idempotency key of at most 80 characters. Retry an uncertain response with the same key and identical payload; conflicting reuse returns 409. The `internal:` prefix is reserved. The browser retains a key for the unchanged command while the page remains open and does not automatically replay failed writes. After a reload, inspect the purchase and ledger before entering another payment. Posting is also uniquely constrained by draft ID.

Posting requires the last draft version. A later payment requires `ExpectedLastEntryId` from the detail response, serializing concurrent allocation changes. Amounts must be positive, two-place PKR and no greater than outstanding. Supplier balance is the sum of signed payable entries: invoice credits positive, payments negative, reversals exact opposites. Signed cash is negative for outflow and positive for a payment reversal. Payment entries contain their purchase allocation directly; there is no standalone unallocated payment in this phase.

Full purchase reversal requires a reason, no net payments, and no later movements in any affected item/warehouse or cylinder serial. It adds exact opposite stock and payable entries. Reverse recorded payments only when correcting an erroneous entry or recording the return of that cash, with an accurate reason. This is not an automatic refund to a supplier or a payment provider. A replacement draft can link to a reversed purchase in the same branch. No original history is deleted or recosted. Inventory's general reversal endpoint rejects purchase receipts, preventing a stock-only correction that would leave the payable intact.

## Permissions and visibility

`purchases.view_cost` controls the purchasing screens, supplier profiles, ledger and voucher, and permits read-only catalog lookups for draft selection. Separate permissions control `suppliers.manage`, `purchases.create`, `purchases.post`, `purchases.pay` and `purchases.reverse`. Payment reversal needs both pay and reverse. Each operation checks current membership permissions and branch grants. Supplier metadata is tenant-wide; financial balances and ledger pages are explicitly scoped to the selected authorized branch. Existing owners receive these permissions on migration; other roles need explicit assignment.

Tenant filters and composite foreign keys protect the new schema. Posted purchase documents and entries have EF append-only guards and PostgreSQL update/delete triggers. PostgreSQL also rejects changes to posted drafts. The purchase document's immutable JSON contains server-created catalog and allocation snapshots; draft JSON is input data revalidated before posting. The normalized stock ledger remains the source of inventory balances.

## Screens and voucher

Use Suppliers to add or edit a profile and review its selected-branch ledger. Use Purchases to create a draft, add lines, save and review server totals, then confirm posting. Detail shows separate document/payment status, costs, cash and correction history. It supports partial payments, reversals, replacement drafts and an authenticated PDF download. Lists and ledgers use server pagination; supplier and catalog selection offer search.

Vouchers are generated in memory using [PDFsharp Core](https://docs.pdfsharp.net/General/Overview/NuGet-Packages.html) with an embedded Bitstream Vera font and its redistribution license. They are returned only through authenticated, branch-authorized, no-store requests; each export is audited. No public file or permanent download URL is created. They include recorded lines, allocations, totals, business date and payment/correction history. They are operational vouchers, not tax invoices. Full Urdu shaping/localized PDF typography remains an open localization decision; the initial template uses English labels and a Latin font.

## Migration and limits

Apply `20260919072004_Purchasing` before starting the new API. It adds Suppliers, PurchaseDrafts, PurchaseDocuments and PurchaseEntries, tenant-qualified keys, immutable-history guards and owner permissions. It does not seed suppliers, invoices, cash or stock and does not change historical valuation. The upgrade check retains Phase 04 stock quantities and costs. Downgrade drops purchasing data and is not an operational recovery method; use backup/restore.

Input bounds: 1–100 lines, at most 1,000,000 units and PKR 1,000,000 unit price per line, at most 1,000 serials per line, and PKR 1,000,000,000 landed charges. Serial-tracked items require registered, distinct and currently unheld serials. The tenant-wide lock prioritizes correctness; reducing its scope needs throughput evidence. Audit, import and reporting expansion remain later phases.
