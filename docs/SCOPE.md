# MVP and release boundary

## Functional MVP: phases 00–08

Include tenant/branch membership, secure identity and permissions; responsive application shell; LPG/cylinder/spare-part catalog; stock ledger and weighted-average costing; suppliers/purchasing; customers/sales/exchanges/receipts; customer and supplier balances; expenses, loans and cash sessions; reconciled dashboard, reports and authorized exports. Use PKR, tenant-configurable locale/time zone and server calculations.

Every implemented workflow must include relevant isolation, audit, idempotency, concurrency and reversal controls. Basic plan/user limits begin in Phase 02. A report requires documented semantics and reconciliation, not just a chart.

## Commercial readiness: phases 09–11

Add operator audit/reversal exploration, validated imports, notifications, complete subscription/onboarding administration, staging and operational hardening. Before real business data is used, satisfy the security, backup/restore and access-control gates applicable to that deployment. Phase 08 marks feature completeness, not unconditional production approval. Phase 12 manages pilot rollout and evidence-driven follow-up.

## Deferred or excluded

- FIFO as an alternate live costing method; preserve an extension point and documented historical cutover.
- Full accounting general ledger, multi-currency, native mobile apps and full offline transaction synchronization.
- Microservices, Kubernetes, Redis and event streaming without evidence and a new decision.
- Autonomous financial posting/pricing, ML services and predictive analytics before reliable data and measured value.
- Delivery dispatch, driver assignment and proof of delivery until the missing delivery scope is approved. Cylinder custody/in-transit state remains part of inventory modeling.
- Live tax/FBR integration and subscription provider integration until requirements/providers are decided.

Tax and legal applicability must be resolved before issuing production invoices. These are open requirements, not declarations that an integration is unnecessary.
