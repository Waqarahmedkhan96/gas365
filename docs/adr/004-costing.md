# ADR 004: Initial inventory costing

Status: accepted from product specification; migration reconciliation requires owner review. Date: 2026-09-16.

## Context

The supplied workbook contains FIFO calculations for LPG and spare parts. The written business specification explicitly chooses perpetual weighted-average cost for the initial release.

## Decision

Use weighted average for new transactions and store immutable COGS snapshots at posting. Receipt cost includes allocated landed costs. Encapsulate costing so FIFO can be added without changing sales commands. Never recompute old posted COGS from today's average.

For example, 10 kg at PKR 100/kg plus 10 kg at PKR 120/kg gives 20 kg at PKR 110/kg. A subsequent 5 kg sale consumes PKR 550 of cost and leaves PKR 1,650 of inventory value before rounding adjustments.

## Alternatives and consequences

FIFO would align more closely with workbook history but is outside the initial written costing choice. Historical FIFO profit and new weighted-average profit may differ legitimately. Opening quantity/value must be reconciled at a documented cutover; do not silently recost source history.

## Implementation gates

Decide costing bucket (tenant/item/location), decimal scales, rounding residuals, backdating and return/reversal valuation before Phase 04 posting is accepted. Prevent zero-denominator and negative-stock cases by default. Test concurrent receipts/issues and reconciliation of stock quantity/value. Negative stock, if later enabled, needs its own defined costing policy.
