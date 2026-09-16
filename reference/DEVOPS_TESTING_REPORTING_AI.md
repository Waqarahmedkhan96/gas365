# DevOps, Testing, Reporting, Automation and AI

## Environments

- Development: local Docker Compose, synthetic seed data, optional local email catcher.
- Staging: production-like configuration, sanitized/synthetic data, automatic deployment after CI.
- Production: protected environment, manual approval, immutable image tags and rollback.

Never share databases or secrets across environments.

## GitHub Actions

Pull-request CI:

1. Frontend install with lockfile, lint, type-check, unit tests and build.
2. Backend restore, format check, build and unit tests.
3. PostgreSQL integration tests and migrations from empty database.
4. Architecture and tenant-isolation tests.
5. OpenAPI generation/validation.
6. Playwright smoke tests against Docker Compose.
7. Dependency, secret and container scans.
8. Build images tagged with commit SHA.

Deployment:

- Main branch -> staging deployment -> smoke tests.
- Version tag/release -> production approval -> backup/precheck -> migration job -> rolling deployment -> health verification -> rollback on failure.
- Never run uncontrolled migrations independently on every API replica.

## Containers

Use multi-stage, non-root Dockerfiles, read-only filesystem where practical, explicit health checks, resource limits, no secrets in images and `.dockerignore`. Compose includes web, API, PostgreSQL and optional development-only mail/object-storage emulators.

## Test pyramid

- Domain unit tests for prices, discounts, costing, payments, balances and cash close.
- PostgreSQL integration tests for EF mappings, constraints, transactions and tenant isolation.
- API contract tests for permissions and validation.
- Component tests for forms/tables.
- Playwright for onboarding, purchase, sale, credit collection, cash close, reversal and export.
- Property/invariant tests: stock ledger balances, allocations, no cross-tenant access, reversal nets to zero.
- Load tests before launch for concurrent sales and report endpoints.

## Reports and downloads

Required reports: daily sales, purchases, stock on hand, stock movement, inventory valuation, LPG profit, spare-parts profit, expenses, gross/net profit, customer aging, supplier payable, cash reconciliation, cylinder custody, branch comparison and user activity.

Every report has a written definition, filter contract and reconciliation test. PDF generation should use an HTML/CSS server template rendered by a supported headless browser or a vetted .NET PDF library. Excel/CSV exports run server-side with permission checks and audit entries. Large exports are background jobs with expiring private download links.

## Automation

- Low-stock alerts
- Overdue credit reminders
- Daily closing reminder
- Backup success/failure notification
- Import/export jobs
- Scheduled report delivery
- Subscription usage alerts

Jobs must be idempotent, retry safely, record attempts and carry TenantId.

## AI roadmap

AI is advisory and opt-in. It never posts financial transactions without confirmation.

Start after reliable historical data exists:

- Reorder suggestions using sales velocity and lead time
- Demand forecasting by item/branch/season
- Margin anomaly and suspicious-discount alerts
- Credit-risk indicators based on payment behaviour
- Duplicate purchase/expense detection
- Natural-language report summaries grounded in computed metrics

Start with deterministic rules and SQL statistics. Add a separate Python FastAPI analytics service only when a trained model provides measurable value. Version models and features, show confidence and explanations, and preserve human approval.
