# ADR 003: Authentication and sessions

Status: accepted and implemented for the local platform in Phase 02. Date: 2026-09-16.

## Decision

Use same-origin frontend/API hosting and ASP.NET Core Identity. Production cookies use the `__Host-` prefix, Secure, HttpOnly and SameSite=Strict; local HTTP development uses a separate cookie name. All unsafe API requests require an antiforgery token, including anonymous login and registration. No browser bearer token storage is used.

Sessions have an eight-hour absolute lifetime and a 30-minute idle limit. PostgreSQL stores revocation and security-stamp binding. Every authenticated request verifies the active membership, session, account status and current role permissions. Membership edits revoke its sessions; password resets revoke all of the account's workspace sessions. Switching workspaces revokes the old session and issues a new one for an owned, active membership. `X-Workspace` checks stale tabs; it never grants tenant access.

Require verified email. Verification/reset links expire after one hour. Five failed attempts lock the account for 15 minutes. The local auth rate limiter allows 60 requests per minute per connection IP. Mailpit captures development messages; production SMTP requires TLS and a configured HTTPS public origin. Cookies and email tokens use ASP.NET Core Data Protection; local keys persist in a dedicated Docker volume.

## Alternatives and consequences

If hosting requires separate origins, revise this decision with an explicit short-lived access token and single-use rotating refresh token design. Durable bearer tokens in localStorage are prohibited. Cookies reduce browser-script access to session credentials but require CSRF protection; XSS remains a risk. Production still requires a shared key store encrypted at rest, trusted proxy configuration and coordinated rate limiting for multiple instances. The local proxy currently shares an IP rate-limit bucket. Account messages are sent inside the provisioning transaction so delivery failure rolls back account creation; reliable queued delivery belongs to Phase 09.

Optional MFA and phone authentication are deferred pending provider/recovery decisions; neither is advertised as available. No production provider or hosting account has been selected. No credentials are seeded; browser tests provision isolated synthetic users.

## Verification

Integration tests exercise antiforgery rejection, production cookie flags, rate limiting, session revocation/idle expiry, lockout, reset replay, membership deactivation, cross-tenant identifiers, workspace switching and live permission changes. Browser tests exercise local email delivery, verification, login, branch creation and logout. See [Phase 02 evidence](../PHASE_02_REPORT.md). Production readiness remains a separate gate.
