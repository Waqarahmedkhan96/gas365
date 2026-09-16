# Phase 02 checkpoint

Date: 2026-09-16. Scope: Identity, Tenancy, Branches and Permissions.

## Delivered

- Email registration, verification/resend, login/logout and password reset using ASP.NET Core Identity; account links are delivered to a local Mailpit inbox.
- Server-stored sessions with revocation, security-stamp validation, absolute/idle expiry and workspace switching through the user's active memberships.
- Tenant settings, branches, team invitations/deactivation, branch grants, owner-controlled role definitions and permission checks on every command.
- Central tenant query filters, tenant-inclusive foreign keys and unique constraints, guarded tenant writes, optimistic concurrency for branch/role/member changes and append-only application audit events.
- Active branch/user/admin quotas checked under a tenant row lock; user input cannot change quota values. Development defaults are 3 branches, 10 users and 3 administrators including the owner, configurable at provisioning. These are not commercial plan commitments.
- Responsive owner/admin/team/branch screens, session listing/revocation and recent activity. Invitation forms default to Worker. All permission decisions remain on the server.
- A separate Compose migration job, persistent local identity keys, query-free proxy access logs and OpenAPI validation for access contracts.

## Acceptance evidence

| Check                                              | Result                                                                                                                       |
| -------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build                              | Passed                                                                                                                       |
| Unit / architecture / PostgreSQL integration tests | 2 / 4 / 26 passed                                                                                                            |
| Frontend lint, TypeScript, build                   | Passed                                                                                                                       |
| Frontend component tests                           | 6 passed, including rejected-login recovery and token removal from history                                                   |
| Browser journeys                                   | 6 passed across desktop and mobile; email verification, login, branch creation, logout, platform health and failure recovery |
| Visual review                                      | Registration and workspace screenshots inspected; mobile viewport has no horizontal overflow                                 |
| Database migrations                                | Empty database, Phase 01 upgrade, repeat application and no model drift                                                      |
| Compose                                            | PostgreSQL, Mailpit, API and web running; migration job exited successfully                                                  |
| OpenAPI                                            | Schema and required health/access paths validated                                                                            |
| Dependency audits                                  | No known vulnerable NuGet or npm dependencies reported                                                                       |

Integration coverage includes every workspace collection endpoint with a foreign context; foreign branch/role/member/session IDs; foreign role/branch references; tenant field injection; scoped lists and audit data; query-filter/write-guard/database-constraint enforcement; denied worker mutations; owner protection; active-member deactivation; cross-workspace switching; live role changes; concurrent branch quota enforcement; administrator/user limits; stale branch versions; reset replay; lockout; session revocation/idle expiry; production cookie flags; CSRF rejection and auth rate limiting. Auth session endpoints are bound to the signed-in membership rather than a caller-provided tenant.

Local evidence is generated under ignored `artifacts/` and `test-results/`. GitHub Actions repeats the acceptance commands and retains its own evidence for each pushed commit. No private workbook, screenshot archive, environment secret or generated build output is included in source control.

## Migration and operating notes

`20260916153300_IdentityAndTenancy` upgrades the empty Phase 01 baseline. It adds no inventory or financial tables. Normal API startup does not migrate; the explicit migration job completes before Compose starts the API. Downgrading removes account/workspace data and is unsuitable as a recovery method for valuable data. Preserve backups before deployment migrations.

Owner permissions include all available permissions. Admin starts with branch/team management and audit visibility; Worker starts without administrative permissions; Auditor starts with audit visibility. Only the owner may define roles, and no member can assign a role above their own privileges. Branch managers see all branches; other members see their active branch grants. No demo credentials are seeded.

## Remaining gates

This is a locally verified foundation, not a production deployment or a full OWASP certification. Production hosting, TLS SMTP, trusted proxy policy, distributed rate limits, protected shared identity keys, database row-level-security evaluation and recovery operations remain release gates. Optional MFA/phone login needs provider and recovery decisions. Email sending is currently synchronous inside the provisioning transaction; durable queued delivery belongs to Phase 09. Plan catalogue/billing, support administration and approved commercial limits remain Phase 10. Full shell/accessibility work is Phase 03; inventory begins in Phase 04.

Next: **Prompt 03 — Gas365 Design System and Application Shell**, only when requested.
