# ADR 005: Posted transaction corrections

Status: accepted. Date: 2026-09-16.

## Decision

Drafts may be edited or deleted subject to permission. Posted financial and inventory transactions retain immutable document and calculation snapshots. Correct by authorized reversal referencing the original, with reason, actor and timestamp, followed by a separately linked replacement when needed.

Reversal applies atomic compensating stock, cost, allocation, balance and cash effects. It must not silently overwrite later history or bypass stock constraints. Closed periods require a controlled reopening or adjustment policy. Reporting must define original/reversed/net treatment consistently.

## Alternatives and consequences

Editing a posted row is simpler but destroys an audit trail and can alter prior reports. Reversal adds workflow complexity and can be blocked by later dependent transactions. Decide partial reversal, downstream stock consumption and closed-period accounting rules before affected modules ship.

## Verification

Test duplicate reversal requests, unauthorized reversal, concurrent reversal/posting, linked replacement and net reconciliation. Verify the original remains unchanged and user-visible document status is independent of payment status.
