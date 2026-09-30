# Urdu interface

Owner decision (2026-09-30): the whole app is translated to Urdu with a right-to-left layout and an English/Urdu switch. The owner or a native speaker reviews the wording before launch.

## How it works

- English is the source text. `apps/web/src/i18n.ts` exports `tr(text, values?)`, which returns the Urdu entry from `apps/web/src/ur.ts` when Urdu is selected. Missing entries fall back to English, so a new screen never shows a blank label.
- Placeholders such as `{name}` or `{count}` are filled after translation, so whole sentences are translated with numbers and names in the right place (for example `Showing {first}–{last} of {total} records · Page {page} of {pages}`).
- The switch (`components/LanguageToggle.tsx`) is in the workspace top bar, the sign-in pages and the website header. The choice is stored in the browser (`gas365:language`) for that device only; the page renders in English if storage is unavailable.
- Urdu sets `<html lang="ur" dir="rtl">`. `AppRoutes.tsx` re-renders pages on a switch and gives Radix components (tabs, menus, dialogs) the page direction. `styles.css` mirrors the sidebar, drawers, search and password icons, tables, timelines and accent borders, flips directional arrows, removes letter spacing (it breaks joined Urdu letters) and uses Urdu-capable fonts after Inter.
- Shared components translate text passed to them as data (page titles, table headers, tabs, status chips, card titles, dialog titles, form labels, sidebar labels).
- Latin digits are kept for amounts and dates, as is common in Pakistani business software. The brand name "Gas365" is never translated.

## Adding or correcting text

1. Wrap new visible text in `tr('English text')`, or `tr('Sentence with {value}', { value })` when it contains data.
2. Add the Urdu entry to `ur.ts` using the exact English text as the key. Keep every placeholder.
3. `src/i18n.test.tsx` fails if an Urdu entry loses or adds a placeholder.

## Terminology used

| English                        | Urdu                          |
| ------------------------------ | ----------------------------- |
| Sale / Sales                   | فروخت                         |
| Purchase                       | خریداری                       |
| Customer                       | گاہک                          |
| Supplier                       | سپلائر                        |
| Stock / Inventory              | اسٹاک                         |
| Warehouse / store              | گودام                         |
| Udhaar / credit                | ادھار                         |
| Cash register / drawer         | کیش رجسٹر / کیش دراز          |
| Draft                          | مسودہ                         |
| Post (a record) / Posted       | درج کریں / درج شدہ            |
| Reverse / Reversal             | ریورس کریں / ریورس اندراج     |
| Deposit                        | ڈپازٹ                         |
| Balance / Payable / Receivable | بیلنس / واجب الادا / وصول طلب |
| Trial                          | آزمائشی مدت                   |
| Refill / Exchange              | ری فل / تبادلہ                |

## Known limitations

- Messages written by the server (validation and error messages, notification titles, report titles and column names, payment instructions) are still English.
- PDF receipts, vouchers and exports remain English.
- Select options built from server values (for example stock states in some forms) show the server word when it has no dictionary entry.
- Translations are machine-assisted and need native review, especially accounting terms.

Covered by `src/i18n.test.tsx` and `tests/Gas365.E2E/urdu.spec.ts` (website and sign-in in Urdu, right-to-left, accessibility, no horizontal overflow, switching back).
