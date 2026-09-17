# Phase 03 checkpoint

Completed on 2026-09-17. Scope: **Gas365 Design System and Application Shell**.

## Outcome and acceptance

- Original Gas365 navy/blue tokens, self-hosted Inter, Tailwind integration and locally owned shadcn-style primitives.
- Responsive sidebar/mobile navigation, header, active branch context, keyboard page search and stable permission-aware URLs.
- Shared page headings, buttons, fields, manual-pagination TanStack tables with mobile cards, loading/error/empty/denied states, confirmation dialogs, detail drawers and notifications.
- Owner-only component showcase at `/workspace/showcase`, using synthetic examples without business mutations.
- Existing identity and workspace administration flows integrated into the shell. Operational modules remain scheduled for later phases.

## Files changed

`apps/web/src/components/`, `lib/`, `navigation.ts` and `ShowcasePage.tsx` contain the shared interface. `WorkspacePage.tsx`, `AuthPage.tsx`, `main.tsx` and `styles.css` integrate it. Frontend manifests, Vite/TypeScript configuration, `components.json`, font dependencies and lockfile define the build. `THIRD_PARTY_NOTICES.md` preserves the adapted Button's MIT attribution.

`apps/web/index.html` and `infra/nginx.conf` pair a per-response style nonce with CSP. Component tests and `tests/Gas365.E2E/` cover behavior and accessibility; Playwright uses two workers. [Interface documentation](DESIGN_SYSTEM.md), local setup, requirement traceability, README and project state describe the delivered checkpoint.

## Decisions and boundaries

Navigation visibility and direct-route checks share one permission map; the backend still authorizes every request. Branch selection remembers only an ID under the tenant's session-storage key, validates it against API-returned active branches and is navigation context until operational modules supply branch-scoped queries. It never changes authoritative stock or totals.

The table accepts a server-page contract rather than calculating business aggregates. Page search covers accessible pages only. The showcase is lazy-loaded. WCAG AA text/control colors use darker semantic variants where the bright brand accents would be unsuitable for text.

## Verification

- `pnpm lint`, `pnpm typecheck`, `pnpm test`: passed, **11 component tests**.
- Production web image build through `node scripts/bootstrap.mjs`: passed; frozen dependencies and Vite production build.
- Backend Release tests: **2 unit, 4 architecture and 26 PostgreSQL/API tests passed**, including tenant isolation, permission changes, fresh/upgrade/repeat migrations and concurrency controls.
- `pnpm test:e2e`: **14 desktop/mobile tests passed**. Real registration, verification, login, branch management and sign-out remain covered. New checks cover branch persistence, rejected foreign IDs, restricted routes/search, keyboard navigation, focus trapping/return, validated fields, pagination, notifications and request recovery.
- Axe WCAG A/AA rules run across authentication, workspace, showcase and interface states; stylesheet-token tests verify contrast. Browser checks include 320 px reflow and dialog CSP console errors. Screenshots were reviewed for desktop and mobile.
- OpenAPI validation, Compose health checks, formatting, dependency audit and Git whitespace checks passed. EF reports no pending model changes.
- **57 tests total.** GitHub Actions repeats the acceptance checks and retains test/service evidence with the pushed commit.

The final rerun required restarting stopped local services. Connection-refused checks before bootstrap were environment failures; acceptance uses the healthy built platform.

## Security and migration impact

No backend endpoint, database model or migration changes. Existing tenant isolation and authorization tests remain applicable and pass. No secrets or supplied source archives are committed. No credentials are stored in browser storage.

Radix scroll locking requires an injected style element. A per-response Nginx nonce permits that style without allowing unrestricted inline styles or scripts. Tests check nonce/header matching, freshness, conditional-request behavior and absence of dialog CSP errors. Alternate hosting must preserve this pairing; see [the deployment contract](DESIGN_SYSTEM.md#content-security-policy). Rebuild web assets and Nginx configuration together. Existing migration procedures are unchanged.

## Limitations and next phase

Automated accessibility and keyboard checks do not replace a full assistive-technology audit. Tenant-configurable locale beyond browser locale remains open; dates retain the tenant time zone and currency remains PKR. Production infrastructure, email, protected keys, recovery and other Phase 02 gates remain outstanding. No production deployment occurred.

Next: **Prompt 04 — Catalog, LPG, Cylinders, Spare Parts and Inventory Ledger**, only when requested. Resolve the recorded costing, rounding, backdating and cylinder-policy decisions before implementing affected business rules. Phase 04 has not started.
