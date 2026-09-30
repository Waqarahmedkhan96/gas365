# Subscriptions, onboarding and platform administration

Phase 10 adds a plan catalogue, business subscription state, server-enforced limits, an onboarding checklist, usage metering, platform administration and owner-approved support access. The owner-approved rules from 2026-09-29 apply:

- three plans;
- a 14-day trial, then read-only;
- a platform administrator configured server-side;
- time-limited support access granted by the business owner.

## Plans and limits

| Plan       | Branches | Users | Administrators |
| ---------- | -------- | ----- | -------------- |
| Starter    | 1        | 3     | 1              |
| Business   | 3        | 10    | 3              |
| Enterprise | 10       | 50    | 10             |

The platform administrator sets prices and can adjust limits and availability. Limits are enforced by the API when branches, members or roles change. Activating a plan copies its limits to the business. A business already above a lower plan's limits keeps everything it has, but cannot add more until it is within the limit.

## Billing periods

Owner decision (2026-09-30): businesses can request a plan billed monthly or yearly. Yearly billing charges ten monthly prices for twelve months (two months free). The server derives `yearlyPrice` (monthly × 10) and `yearlySaving` (monthly × 2) for every plan; clients display these values and never calculate them. A plan request stores the chosen period on the business (`RequestedBillingPeriod`, `Monthly` or `Yearly`), states the amount due in the payment instructions and records the period in the billing history. The platform administrator sees the requested period, and the activation form defaults to 12 months for yearly requests. Activation clears the request. Payment is still confirmed manually.

## Subscription states

- **Trial:** new businesses get 14 days of the Business plan. The dashboard shows the days left to people who can manage the business.
- **Active:** a confirmed payment covers a period (1–24 months, extended from the current end date). Businesses that existed before this phase are pilot customers, grandfathered as Active on the Business plan with no end date.
- **Expired:** evaluated whenever the business is read; no scheduled job is needed. The business stays fully readable, but every change is refused with HTTP 402, apart from these:
  - exporting data;
  - choosing a plan;
  - granting support access;
  - marking notifications read.

  Nothing is deleted.

Owners request a plan on the Subscription page. Payment goes through the `IPaymentProvider` seam. The first provider is **Manual**: the owner pays by bank transfer and the platform administrator records the confirmed amount and reference, which activates the plan. No live payment credentials exist in the system. A future gateway implements the same interface.

The billing history (trial started or extended, plan requested, plan activated) is append-only and visible to the owner.

## Onboarding and usage

The overview shows a getting-started checklist until every step is complete or an owner hides it. The steps are computed from real data:

1. warehouse;
2. items;
3. opening stock;
4. customers;
5. prices;
6. a cash drawer;
7. team invitations;
8. an active plan.

Usage is metered per calendar month in the business time zone. It counts active branches, users and administrators, and the month's sales invoices, purchase invoices, finance entries, stock documents, exports and imported rows. It also estimates storage for export files and import rows.

## Platform administration

The platform administrator is a signed-in user whose confirmed email appears in `Platform:AdminEmails`, a server setting. The local Compose default is `platform-admin@test.example`, overridable with `PLATFORM_ADMIN_EMAILS`. No browser input can grant this role. The Platform admin page lists businesses with plan, status, dates, requested plan and usage counts, but never financial amounts. From it the administrator can:

- record a payment and activate a plan;
- extend a trial by 1–30 days;
- edit plans;
- open an owner-approved support view.

Every administrative action is written into the affected business's own audit log.

## Support access

1. An owner (tenant.manage) grants access for 1 to 72 hours with a reason. Financial figures are excluded unless the owner ticks the option. The owner can revoke the grant at any time.
2. A platform administrator opens the read-only view from an active grant. Only their current session switches into the business, with least-privilege permissions:
   - by default, inventory, customers, sales and activity;
   - with finance included, also purchases, cash, credit, profit and reports.
3. Every workspace request in the view is audited as `support.viewed`, along with `support.entered` and `support.exited`. Every write is rejected with HTTP 403.
4. The view ends when the administrator exits, when the grant expires or is revoked, or when the email is removed from the configuration. The session then returns to the administrator's own workspace. Signing out works from a support view; switching workspace requires exiting it first.

## Migration

Apply `20260930110219_YearlyBilling` (adds the nullable `RequestedBillingPeriod` column with a check constraint). Existing pending requests are treated as monthly.

Apply `SubscriptionsAndPlatform` before starting the updated API. It:

- adds the plan catalogue (seeded with the three plans);
- adds business subscription columns, grandfathering existing businesses as Active on Business;
- creates the append-only subscription history and support grants tables;
- adds a support reference on login sessions.

No posted history changes.
