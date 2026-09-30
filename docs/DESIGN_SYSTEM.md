# Gas365 interface patterns

The Phase 03 shell follows [the UI specification](../reference/UI_SYSTEM.md). It uses Tailwind CSS with locally owned shadcn-style components, Radix interaction primitives and self-hosted Inter. The adapted shadcn Button retains its [MIT notice](../apps/web/THIRD_PARTY_NOTICES.md). See the official [Tailwind Vite integration](https://tailwindcss.com/docs/installation/using-vite) and [shadcn manual setup](https://ui.shadcn.com/docs/installation/manual).

## Preview and navigation

Sign in as an owner and open **Component showcase** from the sidebar, or use **Search pages** (`Ctrl+K` / `Cmd+K`). Its address is `/workspace/showcase`. Search currently navigates accessible pages; it does not search business records. The showcase demonstrates synthetic rows and never posts business data.

Available pages are Inventory, Overview, Branches, Team, Roles, Business settings, Sessions and Activity. Inventory requires its own view permission. Each has a stable URL and browser history. Navigation and direct-route checks share the same permission map in `navigation.ts`; the API remains the authorization boundary. The showcase is owner-only. Future operational modules are listed in a collapsed owner reference section, without actionable routes.

The header lists active branches returned by the API. A selection is remembered in session storage under its tenant ID, and is revalidated against the current branch list. A stale or foreign ID cannot remain selected. No credentials are stored there. Inventory now consumes this branch context for balances and source warehouses; workspace settings still apply to the whole tenant. Inventory APIs separately authorize each affected branch.

## Tokens and components

`apps/web/src/styles.css` defines the original navy/blue palette, surfaces, borders and semantic text colors. Bright success/warning/danger colors are retained as accents; readable text uses darker variants. Standard controls use 8–10 px radii, cards use 12 px, and spacing follows the 4/8/12/16/24/32 scale. Inter is bundled locally, so rendering does not depend on a font CDN.

| Primitive                   | Contract                                                                                                                |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `Button`                    | Primary, secondary, outline, ghost and destructive variants; disabled and visible-focus states; composable link support |
| `Input` / `Field`           | Shared controls; explicit labels, required state, connected help/error messages and invalid state                       |
| `PageHeading` / `PageState` | Breadcrumb, title/action area; loading, error/retry, empty and permission-denied states                                 |
| `DataTable`                 | TanStack Table with controlled manual pagination and row count; table on desktop, labelled record cards on mobile       |
| `Modal`                     | Labelled dialog or detail drawer; focus containment, Escape close and focus return                                      |
| `ConfirmButton`             | Accessible confirmation; initial focus on Cancel; used for sign-out and session termination                             |
| `Notification`              | Dismissible, politely announced success feedback with a seven-second duration                                           |
| `StatusBadge`               | Draft, Posted and Reversed labels that remain understandable without color                                              |

The table receives one page, its total row count and pagination state from its caller. It never calculates business totals or fetches arbitrary tenant data. The showcase uses a small in-memory sample adapter to demonstrate this contract; operational pages will supply server pagination. The showcase is lazy-loaded to keep the main application bundle smaller.

## Accessibility and responsive behavior

- Skip link, semantic navigation/breadcrumbs, active-page indication and focus transfer to the content after navigation.
- Named controls, connected field feedback, labelled dialogs, trapped modal focus and a safe Cancel default.
- Page search accepts keyboard input immediately after opening. Escape restores focus to its trigger.
- Mobile navigation is a dialog; the business name remains visible in the mobile header. Tables become labelled cards, and narrow layouts are checked at 320 CSS px.
- Text/control colors have measured WCAG AA contrast; status badges include words. Focus indicators are visible and reduced-motion preferences are respected.
- Component tests cover errors, confirmation behavior, pagination, permission mapping and actual stylesheet contrast tokens. Browser tests run axe checks against WCAG A/AA rules plus keyboard, focus, viewport and recovery assertions. These checks are evidence for the implemented screens, not a claim that automated testing replaces a full assistive-technology audit.

## Content Security Policy

Radix's scroll-lock helper inserts a style element. Nginx supplies a fresh style nonce in each HTML response and its CSP header; application startup passes it to the helper through [`get-nonce`](https://github.com/theKashey/get-nonce). Nginx [`$request_id`](https://nginx.org/en/docs/http/ngx_http_core_module.html#var_request_id) provides the per-request value and [`sub_filter`](https://nginx.org/en/docs/http/ngx_http_sub_module.html) replaces the HTML placeholder. HTML uses revalidation with ETags and conditional-date responses disabled, preventing nonce/header mismatches. Inline scripts and unrestricted inline styles remain blocked.

Deployment hosts must preserve this HTML/header pairing. The Vite development server does not enforce the production CSP; acceptance browser tests use the built Nginx container. Tests check nonce freshness and pairing, and fail on CSP errors while exercising dialogs and notifications.

## Scope boundary

Phase 03 adds no API endpoints, database columns, migrations, inventory behavior, financial logic or commercial plan rules. Dates retain the tenant time zone. Locale configuration beyond the existing browser locale remains a future settings decision. The remaining production security/recovery gates from Phase 02 still apply.

## Redesign (2026-09-30)

The shell and shared page components were refreshed; see [Interface redesign](UI_REDESIGN.md) for the building blocks and batch status.
