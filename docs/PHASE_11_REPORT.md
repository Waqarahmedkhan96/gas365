# Phase 11 checkpoint

Status: hardening and staging preparation complete and locally verified on 2026-09-29 on `feature/phase-11-hardening`. **No deployment was performed.** Staging needs a server, and production needs explicit owner authorization. Git integration is recorded in [project state](../PROJECT_STATE.md).

## Scope and acceptance

Prompt 11:

- security headers, rate limiting, secret handling, backup/restore runbooks, database retention;
- observability, alerting, performance indexes, load tests, an accessibility audit, and dependency and container scans;
- staging deployment, a restore test in an isolated database, and migrations from the last release;
- a production approval and rollback workflow, and launch-readiness evidence, without deploying production.

Everything that can be done without hosting accounts is complete and verified. Staging deployment is fully prepared but waits for a server. See the [deployment runbook](DEPLOYMENT.md) and [launch readiness](LAUNCH_READINESS.md).

## Owner direction (2026-09-29)

Staging and testing will run in Denmark (EU), and production on a Pakistani server. The domain will probably be `gas365.pk`, decided later. The owner asked for the best setup; the chosen approach is one portable Docker Compose stack with Caddy HTTPS on a Linux VPS in each location. That avoids cloud lock-in, keeps production data in Pakistan, and makes a move between servers a backup restore.

## Delivered

- **Proxy and headers:** the web tier adds X-Frame-Options, Permissions-Policy, COOP and CORP. HSTS comes from Caddy. The API receives forwarded client addresses and trusts them only from private proxy networks.
- **Rate limiting:** a per-user API limit (600/min, configurable) sits on top of the existing credential and session limits.
- **Configuration:** Production validates its configuration after build and refuses to start with missing secrets, an HTTP public origin or a default database password.
- **Retention:** a daily job removes resolved alerts after 180 days, ended sessions after 30 days, and parsed rows of committed imports after 90 days. It never touches ledgers, audit or billing history.
- **Observability:** a `jobs` readiness check reports Degraded (still HTTP 200) for failing or stalled background jobs, and job health is recorded for the retention worker too.
- **Deployment:** `deploy/` contains the production Compose file (no database port, read-only containers, resource limits), the Caddyfile, an environment template, and `deploy.sh` with a pre-deploy backup, one-off migration job, health verification and automatic rollback. `backup.sh` produces verified, pruned dumps with an optional off-site copy and heartbeat; `restore.sh` restores only into a new database.
- **CI and release:** CI adds Trivy image scans and a gitleaks history scan as gates. The Release workflow builds, scans and pushes versioned images, deploys staging automatically after checks pass on main, and deploys production only by manual trigger through the protected production environment. Rollback means redeploying a previous tag.
- **Evidence tools:**
  - `scripts/verify-restore.mjs` restores into an isolated database and migrates the copy;
  - `scripts/load-test.mjs` runs concurrent users through the proxy;
  - `accessibility.spec.ts` audits every owner page;
  - the operations browser test uploads 2,000 rows (about 60 KB) through the proxy.

## Findings fixed during this phase

1. **The web proxy limited request bodies to 32 KB**, so real imports (up to 2 MB) would have failed with 413. The import endpoint now accepts 3 MB at the proxy, and a browser test uploads a large file.
2. **Rate limits would have keyed on the proxy's address**, making the login limit shared by every user on the platform. Trusted forwarded headers now provide the real client address.
3. **The web image had 62 HIGH/CRITICAL fixable vulnerabilities.** OS packages are now upgraded during the build, unused curl is removed, and the runtime uses Alpine's patched nginx (1.28.3-r7). The count is now 0; the API image was already at 0.
4. `/api/health` returned 503 for any non-healthy state. Degraded background jobs no longer make the platform look down; only Unhealthy returns 503.
5. The jobs health check could throw before its table existed and mark readiness Unhealthy. It now degrades instead.
6. Configuration validation first ran before test-host overrides applied. It now runs after `Build()` on the final configuration.
7. The load-test client sent its cookies before fetching the CSRF token. That was a tool bug, fixed before the measured run.

## Verification run in this chat — 2026-09-29

| Check                                          | Result                                                                                                                                                |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build / `dotnet format`        | 0 warnings, 0 errors / passed                                                                                                                         |
| Architecture / unit / PostgreSQL integration   | 4 / 65 / 88 passed (157), including configuration validation, retention, degraded readiness and production-mode startup                               |
| Frontend format, lint, types / component tests | Passed / 23 passed                                                                                                                                    |
| Browser tests (desktop + mobile)               | 30/30 passed, including the 22-page axe audit and the large import through the proxy                                                                  |
| Load                                           | 12 users across 4 businesses, 60 s: 2,255 requests, p95 ≤ 235 ms for every operation, 4 × 429 from the per-user limit (by design with a shared login) |
| Restore                                        | 836 KiB dump of 243 businesses restored into an isolated database; all 9 checked tables equal; 15 migrations applied; copy dropped                    |
| Scans                                          | Trivy: API 0 and web 0 HIGH/CRITICAL fixable findings; gitleaks: no leaks in 23 commits; pnpm and NuGet audits: no known vulnerabilities              |
| Deployment definitions                         | Production Compose renders; Caddyfile valid; workflows pass actionlint; scripts pass shellcheck                                                       |
| OpenAPI / Compose health                       | Passed / healthy after rebuild                                                                                                                        |

No remote CI run, staging deployment or production deployment is claimed.

## Migration impact

No schema migration in this phase. Rebuild both images: the web runtime base changed to Alpine nginx. Configure new settings in non-development environments: `DataProtection:KeyPath`, `Mail:Host`, `Mail:From`, an https `Mail:PublicOrigin` and `Platform:AdminEmails`. Optional settings are `RateLimiting:ApiPerMinute`, `ForwardedHeaders:Limit` and `Retention:WorkerEnabled`.

## Limitations and open items

- Staging and production are not deployed. They need hosting, a domain, SMTP, off-site storage and GitHub environments (see Launch readiness).
- Legal and tax (FBR) decisions and retention periods for financial and audit records are open.
- Optional MFA for owners and administrators is not implemented.
- The load test ran on a developer machine; repeat it on staging.
