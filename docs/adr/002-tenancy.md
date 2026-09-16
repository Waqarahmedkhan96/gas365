# ADR 002: Tenant isolation

Status: accepted. Date: 2026-09-16.

## Context and decision

Use a shared PostgreSQL database initially. Every tenant-owned row includes TenantId; branch-owned transactions include BranchId. Resolve active tenant from authenticated membership, then validate any requested branch. EF filters are a default read boundary, supplemented by command ownership checks, tenant-inclusive unique keys and foreign-key constraints preventing cross-tenant relationships.

Storage paths, exports, durable jobs and audit entries carry the same tenant context. Raw SQL and administrative operations require explicit scope. Platform access is least privilege and audited. Evaluate PostgreSQL row-level security before production; do not claim it is implemented.

## Alternatives and consequences

A database per tenant offers another isolation boundary but increases provisioning and migration overhead. A browser-supplied tenant filter is insufficient. Shared storage lowers initial operating cost but makes missed filters and privileged paths high-impact risks.

## Verification

For every module, test tenant A against tenant B list/get/create/update/delete/post/export endpoints and background execution. Include forged branch IDs, related entity IDs, disabled memberships and concurrent context switching. Database constraints must reject cross-tenant references independently of the UI.
