# ADR 004: Initial inventory costing

Status: accepted from product specification; detailed Phase 04 policy approved by the owner on 2026-09-19. Original date: 2026-09-16. Real-data cutover reconciliation still requires operator review.

## Context

The supplied workbook contains FIFO calculations for LPG and spare parts. The written business specification explicitly chooses perpetual weighted-average cost for the initial release.

## Decision

Use weighted average for new transactions and store immutable COGS snapshots at posting. Receipt cost includes allocated landed costs. Encapsulate costing so FIFO can be added without changing sales commands. Never recompute old posted COGS from today's average.

For example, 10 kg at PKR 100/kg plus 10 kg at PKR 120/kg gives 20 kg at PKR 110/kg. A subsequent 5 kg sale consumes PKR 550 of cost and leaves PKR 1,650 of inventory value before rounding adjustments.

## Alternatives and consequences

FIFO would align more closely with workbook history but is outside the initial written costing choice. Historical FIFO profit and new weighted-average profit may differ legitimately. Opening quantity/value must be reconciled at a documented cutover; do not silently recost source history.

## Implementation gates

Decide costing bucket (tenant/item/location), decimal scales, rounding residuals, backdating and return/reversal valuation before Phase 04 posting is accepted. Prevent zero-denominator and negative-stock cases by default. Test concurrent receipts/issues and reconciliation of stock quantity/value. Negative stock, if later enabled, needs its own defined costing policy.

## Phase 04 accepted policy

The implementation uses a product/warehouse cost pool across stock states, configurable unit precision up to 3 places, 6-place cost snapshots and 2-place PKR movement values, midpoint away from zero. Full depletion consumes the remaining value. The owner explicitly approved this numeric/cost-pool policy on 2026-09-19 for current use, with changes to be requested later if needed. This closes the Phase 04 costing acceptance gate. Future policy changes require a documented effective scope, tests and migration/reconciliation assessment; they must not silently recalculate posted costs. Future return valuation and real-data cutover remain separate decisions.

The owner selected current-date posting only and full reversals only where no later affected stock movement exists. Reversals preserve original values exactly. Historical FIFO data is not imported; an operator must reconcile each opening quantity/value against a cutover reference. See [inventory contracts](../INVENTORY.md).
