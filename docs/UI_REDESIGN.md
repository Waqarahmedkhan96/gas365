# Interface redesign

Owner request (2026-09-30): improve almost every page, following the structure and actions of the supplied GasFlow screenshots in the Gas365 theme. The screenshots guide layout and interaction only. Gas365 keeps its own name, logo, wording and icons. The redesign changes presentation only. It adds no API, database or business-rule changes.

## Approved plan

The owner approved the plan on 2026-09-30. Work runs on `feature/ui-redesign` in batches, and the owner reviews screenshots after each batch.

| Batch                | Scope                                                                                                                                                                                                                                                               | Status                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------- |
| 1. Foundation        | Grouped, collapsible sidebar; top bar with business, branch, search, alerts and account menu; page header with icon and actions; summary cards, status chips, section titles, cards, list toolbar, empty/error states; table footer; sign-in and registration pages | Complete                        |
| 2. Core daily work   | Dashboard; sales list, full-page sale entry with summary panel, sale details and receipt; customers list and profile                                                                                                                                                | Complete                        |
| 3. Stock and buying  | Inventory, price lists, purchases, suppliers                                                                                                                                                                                                                        | Not started                     |
| 4. Money             | Cash register, expenses, udhaar, loans and owner funds                                                                                                                                                                                                              | Not started                     |
| 5. Reports and admin | Reports (profit and loss layout, day-end cash), subscription plan cards, settings, branches, team, roles, sessions, notifications, imports, activity, platform admin                                                                                                | Not started                     |

These are not included unless the owner asks, because each needs new functionality: Urdu interface, Google sign-in, public marketing website, onboarding wizard, yearly billing and add-ons, and a separate spare-parts report.

## Shared building blocks

- Icons: Lucide (`lucide-react`, ISC licence; see `apps/web/THIRD_PARTY_NOTICES.md`). Page icons are mapped in `apps/web/src/components/icons.tsx`. The Gas365 mark is an original SVG.
- Navigation: each page in `navigation.ts` declares a `group`. `pageGroups` and `groupOrder` define the sidebar sections and their order. Permission checks are unchanged (`canVisit`).
- Page pieces (`components/page.tsx`): `PageHeading` (icon, title, description, actions), `StatGrid` and `StatCard`, `StatusChip` (always icon plus text; tones neutral/info/success/warning/danger), `StatusBadge`, `SectionTitle`, `Card`, `Toolbar` (labelled search plus filters) and `PageState`.
- Confirmations: `ConfirmDialog` is the controlled form of `ConfirmButton`. It is used for actions started from menus, such as signing out from the account menu.
- The sidebar collapse preference is kept in browser storage for that viewer only; the page works without it.

## Accessibility notes

- The branch selector keeps its "Active branch" name when its label is hidden visually on narrow screens.
- The top bar is sticky on desktop only. On small screens it scrolls away so it never covers controls, and desktop pages keep scroll padding for focused elements.
- Links inside sentences are underlined. The account menu button's name starts with the visible user name.

## Batch 2 notes

- Pages place primary actions next to the title through `PageActions` (`components/page-actions.tsx`), which renders into a slot owned by the workspace heading.
- Overview: filter bar, summary cards with icons and tones, daily trend, recent sales (posted sales for the active branch), quick actions and workspace details. The period filter adds "Today".
- Sales: summary cards (today's net revenue and customer receivables from the server dashboard when the user can view reports, plus posted and draft counts), tabs for posted sales, drafts and borrowed cylinders, a full-page draft editor with a sticky summary panel, and full-page draft and sale details. Totals still come only from the server; the editor summary lists lines and quantities without computing money.
- `/workspace/sales?new=1` opens the draft editor directly (used by Overview quick actions and the customer profile).
- Customers: search toolbar, customer cards, add/edit in a dialog, and a full-page profile with balance cards from the server credit ledger (users with finance access; positive means the customer owes), plus tabs for sales, borrowed cylinders and details.
- Shared helpers moved to `lib/ui-helpers.ts`: `statusTone` (status word to chip tone) and `useViewFocus` (moves focus to a newly opened full-page view).

## Test changes in Batch 2

- The customer and sales journey returns to lists with "Back to customers" / "Back to sales" and opens the "Borrowed cylinders" tab before cylinder steps. The assertions themselves are unchanged.
- The empty customer list uses "Add your first customer", so the header's "Add customer" stays unique.

## Test changes in Batch 1

- The identity journey signs out through the account menu (`… account menu` → menu item `Sign out` → `Confirm`), because Sign out moved into that menu.
- The showcase pagination expectation now reads `Showing 3–3 of 3 records · Page 2 of 2`.
