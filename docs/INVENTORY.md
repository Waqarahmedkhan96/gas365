# Inventory implementation and operating rules

Phase 04 adds tenant-owned catalog and inventory data under `/api/workspace/inventory`. PostgreSQL is authoritative. No sales, purchasing, payment, deposit or tax module is included.

## Catalog and locations

Owners receive `inventory.view`, `catalog.manage`, `inventory.post` and `inventory.reverse` through the upgrade migration. Other roles receive only the permissions an owner assigns. Viewing stock does not grant permission to post or reverse it. A user must also have access to every branch affected by a command; transfers require access to both ends.

Create units with 0–3 decimal places, spare-part categories, and warehouses associated with an existing branch. A warehouse includes a location/rack description. Separate warehouses represent independently costed locations; changing descriptive rack text does not move stock. Each item has a unique tenant SKU and a unit. LPG and cylinders record nominal capacity, brand and Domestic/Commercial classification. Cylinders use whole-number units and optionally track individual serials. Spare parts require a category. Reorder levels use the unit's precision.

SKU, kind, unit and serial policy are immutable after creation. Correct those identities by creating another SKU. Display name, brand, reorder level and active status can be edited with optimistic concurrency; the API also supports metadata and warehouse updates. Units and categories are append-only configuration in this phase. Catalog and asset metadata edits record audit events. No endpoint hard-deletes inventory records.

## Quantities, states and valuation

The valuation policy, approved by the owner on 2026-09-19, is a separate perpetual weighted-average pool for each tenant/product/warehouse. This is the accepted policy for current use; later requested changes require a documented decision and impact review that preserves posted history. Quantities use the configured unit precision, up to 3 places. Receipt unit costs allow 6 places, PKR values use 2, and midpoint rounding is away from zero. Negative stock is prohibited.

Stock quantity in each warehouse/state is the sum of signed ledger quantities. Warehouse value is the sum of signed ledger values across all states for that product. Average cost is warehouse value divided by warehouse quantity, displayed to 6 places. Issues use the unrounded ratio to calculate the 2-place amount; the final issue consumes the remaining value exactly. A receipt's amount is quantity multiplied by entered unit cost, rounded to 2 places. The server accepts authorized receipt cost inputs, validates them and calculates the amounts; an issue or transfer cannot supply its own cost.

State quantities are separate from the cost pool. `WarehouseValue` and `AverageCost` in balance rows are repeated warehouse-level context; **do not add WarehouseValue across state rows**. A state balance is not separately valued. A transfer moves exactly the source issue value to the destination, where it joins that warehouse's cost pool. Moving between states in one warehouse preserves its quantity and value.

Non-cylinder states are Available and Damaged. Cylinder states are Full, Empty, InTransit, CustomerHeld, Inspection, Damaged, Lost and Retired. Moving an asset to a non-available state records custody/status; it does not automatically write off its value or create income/expense accounting. A reviewed adjustment removes quantity/value when necessary. Customer identity, exchanges and deposits belong to later sales workflows. No fill calculation converts cylinder quantities into LPG quantities.

## Posting and corrections

The owner selected current-date posting only, optional per-item serial tracking, and full reversal only when no later affected stock movements exist. Business date comes from the tenant's IANA time zone; the timestamp is UTC. The command has no editable posting-date field.

Supported commands are Opening, AdjustmentIn, AdjustmentOut and Transfer. Every command needs a reason. Opening balances require a source reconciliation reference and are allowed only before any movement in that warehouse/state. They establish new weighted-average stock; they do not import or recost historical FIFO transactions. An opening that has been reversed remains historical, so use an explicitly documented adjustment or a new item/location instead of deleting its history.

A posted document and its signed movements are immutable in EF and through PostgreSQL update/delete triggers. Reversal creates a separate document with exact opposite quantities/values and a reference to the original. Reversed status is derived from that relationship. Reversals cannot themselves be reversed; neither partial reversal nor historical recosting is provided. Any later movement in an affected product/warehouse prevents automatic reversal, even if that later movement was subsequently reversed. Use a reviewed adjustment in that case. Serial-wide dependencies are checked across warehouses too.

Commands acquire a tenant row lock before reading balances. This deliberately serializes inventory writes within one tenant, including absent buckets and cross-warehouse transfers, while different tenants have independent locks. All document, movement and audit writes commit together. Ledger balances are grouped in PostgreSQL; the stock directory and ledger use server pagination. Optimize locking granularity only after concurrency measurements justify it.

## API and import-ready contracts

Use the same-origin session cookie, CSRF token and `X-Workspace` header. The client keeps its CSRF request token only in memory and renews it after authentication changes or rejected requests, avoiding a token fetch for every stock command. Failed commands are never automatically replayed. Tenant identity is resolved from the authenticated membership, and the header must match it. Never supply TenantId or authoritative totals in a payload. Composite keys and foreign keys include TenantId; warehouse movement references additionally bind BranchId.

Read endpoints cover units, categories, paginated items, an item, visible warehouses, item balances, a paginated item ledger, assets and a stock document. Create endpoints cover configuration, items, warehouses, assets and stock commands. Item/warehouse/asset updates use the returned `Version` value. Full reversal has its own command. OpenAPI describes these DTOs, and endpoint handlers delegate business rules to application services.

Example posting payload (replace the UUIDs with IDs returned by the API):

```json
{
  "idempotencyKey": "opening-cutover-warehouse-a-lpg-001",
  "kind": "Opening",
  "itemId": "11111111-1111-1111-1111-111111111111",
  "warehouseId": "22222222-2222-2222-2222-222222222222",
  "bucket": "Available",
  "quantity": "10.000",
  "unitCost": "100.000000",
  "destinationWarehouseId": null,
  "destinationBucket": null,
  "reason": "Reconciled opening worksheet reference OPEN-001",
  "expectedSequence": 0,
  "destinationExpectedSequence": 0,
  "serials": []
}
```

Decimal strings preserve input precision. `ExpectedSequence` is the latest returned sequence for the whole product/warehouse, not just one state. Transfers also require the destination sequence. A new pool has sequence zero. A stale sequence returns 409; refresh before preparing a new command. Use the same key and exact payload when retrying an uncertain response. A completed key returns its original document; changing its payload returns 409. Keys are unique across posting and reversal commands within the tenant. Never automatically generate a new key merely because the response was lost.

A command accepts at most 1,000,000 units, a receipt cost up to PKR 1,000,000 per unit, and at most 1,000 serials. These are input safety bounds rather than commercial limits. Large openings can be split into a first Opening and subsequent referenced adjustments. Future import tooling must call validated application commands with stable source keys, map units/SKUs explicitly and reconcile quantities/values. Bulk file upload/import execution is Phase 09, not a direct database path.

Serials must be registered for the cylinder item before posting, are normalized to uppercase, and are unique within the tenant. Every serial-tracked command supplies one distinct serial per unit. Signed serial movements derive current custody; duplicates and unavailable source serials are rejected. Asset records retain owner text and inspection reference/due date; customer/deposit relationships remain deferred.

## Local use and deployment

Run bootstrap, sign in as an owner, then open Inventory. Use Catalog setup to create units, categories and warehouses. Add an item, select it in the directory and post its reconciled opening. The active branch controls balances and source warehouses; destinations include the user's other authorized branches. Inspect a ledger document to reverse it when permitted.

`InventoryLedger` creates catalog, location, serial, document and movement tables. `InventoryGuards` adds immutable-ledger triggers and updates existing owner permissions. Apply both with the one-shot migration job or the documented migration command before starting the new API. No seed inventory or guessed opening values are inserted. Upgrade tests retain prior tenant data and permissions. Down migrations remove inventory and its protections: use backup/restore for recovery rather than downgrading valuable data.

On this Windows machine, port 55432 became reserved by the operating system; the ignored local `.env` now uses 54320. The default example remains 55432 and is configurable. No database volume or credentials were replaced.
