# ADR 003: Authentication and sessions

Status: accepted with hosting topology gate before Phase 02. Date: 2026-09-16.

## Decision

Prefer same-origin frontend/API hosting and ASP.NET Core Identity with Secure, HttpOnly, SameSite session cookies, antiforgery checks and server-controlled session revocation. Add lockout, rate limits, verification/reset flows, session listing and optional MFA. Permissions are rechecked by the API independently of visible navigation.

## Alternatives and consequences

If approved hosting requires separate origins, revise this decision with an explicit short-lived access token and single-use rotating refresh token design. Durable bearer tokens in localStorage are prohibited. Cookies reduce browser-script access to session credentials but require CSRF protection; XSS remains a risk. Multi-instance deployments must share protected key material and revocation state appropriately.

## Verification

Test missing/invalid antiforgery tokens, session revocation, lockout, reset expiry, membership removal and denied permissions. Decide session lifetimes, verification provider and MFA rollout in Phase 02. No live identity integration exists yet.
