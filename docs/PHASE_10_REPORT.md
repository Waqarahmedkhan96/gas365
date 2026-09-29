# Phase 10 checkpoint

Status: implementation and required local checks complete on 2026-09-29 on `feature/phase-10-platform`. Git integration is recorded in [project state](../PROJECT_STATE.md).

## Scope and acceptance

Prompt 10:

- a plan catalogue, tenant subscription state and server-enforced limits;
- an onboarding checklist and usage metering;
- platform administration, with support access that is least-privilege, time-bounded and audited;
- no exposure of tenant financial data by default;
- a payment-provider abstraction with no invented live credentials.

All criteria are met. The operating contract is in [subscriptions, onboarding and platform administration](PLATFORM.md).

## Owner decisions (2026-09-29)

- **Plans:** Starter (1 branch, 3 users, 1 admin), Business (3, 10, 3) and Enterprise (10, 50, 10). The platform administrator sets prices later.
- **Trial:** 14 days on Business, then read-only until a plan is chosen. Nothing is deleted, and exports remain available.
- **Super admin:** configured server-side by email (`Platform:AdminEmails`), never self-registered.
- **Support access:** granted by the owner, time-limited, read-only and audited. Financial figures are excluded unless the owner includes them.

## Delivered

- **Plans and subscriptions:**
  - a seeded plan catalogue;
  - tenant status (Trial, Active or Expired), evaluated when read so no scheduled lock job is needed;
  - the 402 read-only guard with a documented allow-list;
  - activation that applies the plan's limits;
  - an append-only billing history.
- **Payments:** the `IPaymentProvider` seam, with a Manual (bank transfer) provider. The owner requests a plan; the administrator records the confirmed payment and activates it.
- **Onboarding and usage:** a checklist computed from real data, and monthly usage metering (counts and storage estimate).
- **Platform administration:** a business list with counts only, plan editing, activation, trial extension and the active support grants. Every action is written to the affected business's audit log.
- **Support views:** switch only the administrator's current session into the business with least-privilege permissions. Every request is audited, and writes are rejected. The view ends on exit, expiry, revocation or removal of the admin email. Logout works in the view; switching workspace requires exiting first.
- **Screens:** Subscription (plan, trial countdown, usage against limits, plans, history, support grants), a getting-started checklist, trial and read-only banners, the support-view banner with exit, and the Platform admin page.

## Corrections during verification

1. After choosing a plan, the payment instructions were cleared immediately by the generic success handler. A component test caught this; messages are now only replaced when the action supplies one.
2. The platform component test's "no finance shown" assertion was sharpened to money figures, because "PKR" legitimately appears in a form label.
3. The persistent trial, read-only and support banners do not use the live `status` role, so screen readers do not re-announce them and existing status-based browser checks stay unambiguous.

## Verification run in this chat — 2026-09-29

| Check                                                 | Result                                                                                                                                                                                                                                                                  |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend Release build / `dotnet format`               | 0 warnings, 0 errors / passed                                                                                                                                                                                                                                           |
| Architecture / unit / PostgreSQL integration          | 4 / 63 / 86 passed (153). New: trial expiry and read-only allow-list; platform authorization, activation limits and owner-visible audit; support grants that are read-only, least-privilege, audited and ended on revoke; onboarding and usage; extended migration test |
| Frontend format, lint, types, build / component tests | Passed / 23 passed                                                                                                                                                                                                                                                      |
| Browser tests (desktop + mobile)                      | 28/28 passed, including the owner and platform-admin two-context journey with axe                                                                                                                                                                                       |
| Migrations                                            | Fresh, upgrade (grandfathering asserted), repeat and model drift; applied locally by the Compose migrate job                                                                                                                                                            |
| OpenAPI / Compose / audits                            | Passed / healthy / no known vulnerabilities                                                                                                                                                                                                                             |

No remote CI or production deployment is claimed.

## Migration impact

Apply `20260929142905_SubscriptionsAndPlatform` before starting the updated API. It seeds the three plans and grandfathers existing businesses as Active on Business with no end date and unchanged limits. It adds subscription columns, the append-only subscription history, support grants and a session support reference. Compose sets `Platform__AdminEmails` from `PLATFORM_ADMIN_EMAILS` (default `platform-admin@test.example`); set the real address per environment.

## Limitations

- There is no live payment gateway, invoice PDF or tax handling for subscription billing; payments are confirmed manually.
- Monthly transaction limits are metered but not enforced; the owner has not set any.
- The platform administrator is configured by email; MFA for administrators is a Phase 11 hardening item.
