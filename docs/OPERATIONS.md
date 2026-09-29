# Imports, opening balances, notifications and audit

Phase 09 adds validated Excel and CSV imports, opening customer and supplier balances, in-app notifications, an audit timeline, a corrections explorer and background-job health. The owner-approved rules from 2026-09-29 are applied:

- Imports cover master data plus opening stock and opening balances.
- Credit is overdue after 30 days, adjustable per business.
- Notifications are in-app; email comes later.
- The closing reminder is at 21:00, adjustable per business.

## Opening balances

An opening balance records credit that existed before Gas365, for one customer or one supplier in one branch. It stores the original invoice date for aging. Posting uses the current business date, so there is no backdating. It moves no cash.

- **Create:** requires finance.manage and a command key. The original date must be in the past and after 2000; the amount must be positive with two decimal places.
- **Settle:** record the payment (a customer receipt or supplier payment), then allocate it to the opening balance, as for any invoice. An allocation cannot exceed what is outstanding.
- **Reverse:** requires finance.reverse and finance.manage. Reverse its allocations first. The reversal is a separate entry with a reason.
- **Integrity:** the documents and their journal are append-only in the application and in database triggers.
- **Where they appear:** in the Udhaar credit view, customer aging and supplier payables reports, the supplier ledger balance, dashboard receivables and payables, and overdue alerts.

## Imports

Access requires imports.manage plus the permission of the target data: customers.manage, suppliers.manage, catalog.manage, inventory.view and inventory.post, or finance.manage.

| Type                                 | Columns (required in bold)                                                                                                   | Notes                                                                                                                                                          |
| ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Customers / suppliers                | **Name**, Phone, Address                                                                                                     | A name and phone matching an existing record, or an earlier row, is rejected as a duplicate.                                                                   |
| Catalog items                        | **SKU**, **Name**, **Type**, **Unit**, Category, Brand, Classification, Capacity, Tare weight, Serial tracked, Reorder level | SKUs must be new. Units and categories must exist. LPG and cylinders need a capacity and a Domestic or Commercial classification. Spare parts need a category. |
| Opening stock                        | **SKU**, **Warehouse**, State, **Quantity**, **Unit cost**, Serial numbers                                                   | One opening per item, warehouse and state. Serial numbers (separated by semicolons) must be registered first.                                                  |
| Opening customer / supplier balances | **Customer** or **Supplier**, **Branch**, **Amount**, **Original invoice date**, Reference                                   | Names must match exactly one active record. Dates may be yyyy-mm-dd or dd/mm/yyyy.                                                                             |

1. **Upload.** Upload `.xlsx` or UTF-8 `.csv`, up to 2 MB, 5,000 rows, 50 columns and 1,000 characters per cell. The content must match the extension: an XLSX file must be a real workbook, and a CSV must be text without binary bytes. Legacy `.xls` files are rejected with instructions. The file is parsed into rows stored with the batch; the raw file is not kept. Excel binary doubles are rounded to Excel's 15 significant digits.
2. **Map.** Columns are matched automatically by name and common aliases. The operator can change the matching until the import starts. Each column maps to at most one field.
3. **Validate and preview.** Every row is checked against current data and against earlier rows in the file. The preview lists rows with problems first. The error list downloads as CSV (row, field, error), with cells protected against spreadsheet formula injection.
4. **Commit.** A version check and an atomic claim stop two commits from interleaving; an abandoned commit can be resumed after ten minutes. Rows are validated again first, and changed data stops the commit with the errors shown. Each row then runs through the normal command:
   - master data commits in one transaction with its row status;
   - opening stock and opening balances use the key `import:<batch>:<row>`, so a retry replays instead of duplicating.
5. **Resume.** A row that fails at commit (for example an unregistered serial number) is marked Failed with the reason, and the batch becomes Partly imported. After the cause is fixed, committing again processes only rows that have not been imported.

Batches are private to the member who uploaded them. Uploading, validating and committing are audited.

## Notifications

A sweeper runs every 60 seconds (`Notifications:SweepSeconds`; the local Compose setting is 15). It rebuilds the alerts each business needs and compares them with active alerts: new conditions are added, changed text is refreshed and cleared conditions are resolved. It is idempotent, and a PostgreSQL advisory lock per tenant stops several API instances duplicating work. A partial unique index enforces one active alert per condition.

| Alert               | Raised when                                                                                                                                       | Visible to     | Resolves when                           |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- | --------------------------------------- |
| Low stock           | An active item with a reorder level has sellable stock (Available or Full) at or below that level in a branch that has held it                    | inventory.view | Stock is replenished above the level    |
| Overdue credit      | A customer's sale invoices, cylinder settlements or opening balances are unpaid more than the configured days (default 30) after the invoice date | finance.view   | Paid, allocated, or the setting changes |
| Register still open | A drawer session remains open after the reminder time (default 21:00) on its day, or on any earlier day                                           | register.close | The register is closed                  |

Members see only alerts whose permission they hold, for branches they can access. Read state is tracked per person. The header bell shows the unread count and refreshes every minute. Owners change the two settings under Business settings.

## Audit timeline and corrections

- **Timeline:** audit events for up to 366 days in the tenant time zone. Filter by area (access, inventory, purchasing, sales, finance or reports), actor or record. Actors are named only when they are current or former members.
- **Corrections explorer:** every reversal across stock, sales, purchasing, finance, cylinders and opening balances. Each shows the original record, amount, reason, author, branch and date. Filter by module and branch.
- **Access:** both require audit.view.

## Job health

Background jobs record their last start, success, failure, error and consecutive-failure count in `JobStates`:

- the notification sweep records every run;
- the export worker records when it processes work or fails.

Failed exports retry up to three attempts and then fail with a reason. A failed sweep retries on the next interval. Platform monitoring of these rows is Phase 11 scope.

## Migration

Apply `AuditImportsAndNotifications` before starting the updated API. It:

- adds the business notification settings, defaulting existing tenants to 30 days and 21:00;
- adds the opening-balance tables and triggers, a finance-entry reference, and the notification, import and job-health tables;
- grants imports.manage to owners.

No posted history changes.
