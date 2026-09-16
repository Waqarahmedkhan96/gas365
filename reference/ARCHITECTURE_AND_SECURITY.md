# Architecture and Security

## Initial production topology

```text
Browser/PWA
  -> CDN or reverse proxy with HTTPS
  -> React static application
  -> ASP.NET Core modular-monolith API
  -> PostgreSQL
  -> private object storage for generated/imported files
  -> background worker using a durable database-backed queue initially
```

The application server and database may be separate Docker services for development. For production, use a managed PostgreSQL service if affordable; otherwise operate a separate database host with encrypted backups and tested recovery. Do not colocate irreplaceable production data only inside an application container.

## Solution layout

```text
apps/web
src/Gas365.Api
src/Gas365.Application
src/Gas365.Domain
src/Gas365.Infrastructure
tests/Gas365.UnitTests
tests/Gas365.IntegrationTests
tests/Gas365.ArchitectureTests
tests/Gas365.E2E
docs
infra
```

Modules: Identity, Tenancy, Branches, Catalog, Inventory, Purchasing, Sales, Customers, Suppliers, Credit, Cash, Expenses, Loans, Deliveries, Reporting, Subscriptions, Audit and Notifications.

## Tenant isolation

- TenantId on every tenant-owned aggregate and transaction.
- BranchId on branch-owned records.
- Tenant context resolved from the authenticated membership, never from an arbitrary request field.
- Central EF query filters plus command-level ownership checks.
- Composite unique keys and indexes include TenantId.
- PostgreSQL row-level security should be evaluated as defence in depth before production.
- Cross-tenant negative integration tests for list, get, update, delete, export and background jobs.
- Storage paths, cache keys, job payloads and audit entries include tenant context.

## Authentication and sessions

Preferred for a same-origin web deployment: ASP.NET Core Identity with secure, HttpOnly, SameSite cookies, antiforgery protection and server-side session revocation. If separate origins force bearer tokens, use short-lived access tokens plus single-use rotating refresh tokens in secure HttpOnly cookies. Never keep durable tokens in localStorage.

Include verified email/phone workflow, password reset, lockout, rate limiting, optional MFA, device/session listing and forced revocation.

## Authorization

Use policies backed by permissions such as `sales.create`, `sales.void`, `sales.change_price`, `inventory.adjust`, `purchases.view_cost`, `reports.view_profit`, `cash.close_day`, `users.manage` and `branches.manage`.

Frontend capability checks improve usability; the API remains authoritative.

## Financial integrity

- Atomic database transactions for posting workflows.
- Idempotency key for create/post endpoints.
- Decimal precision defined explicitly in EF configuration.
- Posted documents have immutable numbers and snapshots.
- Reversal records reference the original transaction.
- Database constraints prevent negative/invalid states where appropriate.
- Concurrency tokens protect day closing, stock adjustment and payment allocation.

## Security baseline

- HTTPS/HSTS, CSP, strict CORS, security headers and secure cookies.
- Request validation, output encoding and parameterized database access.
- Rate limits on authentication, exports and expensive reports.
- Malware/content validation for uploads.
- Secrets from environment/secret manager; rotate credentials.
- Dependency, secret, SAST and container scanning in CI.
- Encrypted backups with documented retention and restore drills.
- Structured audit records for logins, permission changes, pricing overrides, postings, reversals, exports and support access.
- Avoid sensitive values in logs and telemetry.

## Scale path

Make the API stateless from the start. Later add a load balancer and multiple API/worker instances. Add Redis only after measurements show database/cache pressure or distributed coordination requires it. Use managed PostgreSQL, connection pooling, indexes, query analysis and read replicas before inventing distributed services.
