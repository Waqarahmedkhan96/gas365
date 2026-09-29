using System.Globalization;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

internal sealed record ImportField(string Key, string Label, bool Required, string Hint, string[] Aliases);
internal sealed record ImportDefinition(string Key, string Title, string Description, string[] Permissions, ImportField[] Fields);
internal sealed record ImportError(string Field, string Message);

// Upload → map → validate/preview → commit. Every row is committed through the same validated command a person
// would use; nothing is written to a posted ledger directly.
public sealed class ImportAccess(Gas365DbContext db, TenantScope scope, IInventoryCosting costing) : IImportAccess
{
    private static ImportField F(string key, string label, bool required, string hint, params string[] aliases) => new(key, label, required, hint, aliases);

    internal static readonly ImportDefinition[] Catalog =
    [
        new("customers", "Customers", "Customer contacts. Rows matching an existing customer's name and phone are rejected as duplicates.", [Permissions.CustomerManage],
            [F("name", "Name", true, "Up to 120 characters.", "customer", "customername"), F("phone", "Phone", false, "Up to 80 characters.", "mobile", "contact"), F("address", "Address", false, "Up to 300 characters.")]),
        new("suppliers", "Suppliers", "Supplier contacts. Rows matching an existing supplier's name and phone are rejected as duplicates.", [Permissions.SupplierManage],
            [F("name", "Name", true, "Up to 120 characters.", "supplier", "suppliername"), F("phone", "Phone", false, "Up to 80 characters.", "mobile", "contact"), F("address", "Address", false, "Up to 300 characters.")]),
        new("items", "Catalog items", "LPG, cylinder and spare-part items. Units and categories must already exist; SKUs must be new.", [Permissions.CatalogManage],
            [F("sku", "SKU", true, "Unique item code.", "code", "itemcode"), F("name", "Name", true, "Item name.", "item", "itemname", "description"),
             F("kind", "Type", true, "LPG, Cylinder or Spare part.", "itemtype", "category type"), F("unit", "Unit", true, "Existing unit code, such as KG or PCS.", "uom"),
             F("category", "Category", false, "Existing category name; required for spare parts."), F("brand", "Brand", false, ""), F("classification", "Classification", false, "Domestic or Commercial."),
             F("capacityKg", "Capacity (kg)", false, "Cylinders only.", "capacity"), F("tareWeightKg", "Tare weight (kg)", false, "Cylinders only.", "tare", "tareweight"),
             F("serialTracked", "Serial tracked", false, "Yes or No; cylinders only.", "serial"), F("reorderLevel", "Reorder level", false, "Zero or more.", "reorder", "minimum")]),
        new("opening-stock", "Opening stock", "Counted stock per warehouse and state at cutover, valued at your reconciled unit cost. Items and warehouses must exist; register serial numbers first.", [Permissions.InventoryView, Permissions.InventoryPost],
            [F("sku", "SKU", true, "Existing item SKU.", "code", "itemcode"), F("warehouse", "Warehouse", true, "Existing warehouse name.", "store", "location"),
             F("state", "Stock state", false, "Available or Damaged; for cylinders Full, Empty, Inspection or Damaged. Defaults to Available or Full.", "bucket", "status"),
             F("quantity", "Quantity", true, "Positive, in the item's unit.", "qty"), F("unitCost", "Unit cost (PKR)", true, "Reconciled cost per unit.", "cost", "rate", "price"),
             F("serials", "Serial numbers", false, "Separate with semicolons; one per cylinder for serial-tracked items.", "serial")]),
        new("customer-balances", "Opening customer balances", "Credit owed by customers at cutover. Aging starts from the original invoice date.", [Permissions.FinanceManage],
            [F("customer", "Customer", true, "Existing active customer name.", "name", "customername"), F("branch", "Branch", true, "Existing branch name."),
             F("amount", "Amount (PKR)", true, "Positive, two decimal places.", "balance", "due", "outstanding"), F("originalDate", "Original invoice date", true, "yyyy-mm-dd or dd/mm/yyyy.", "date", "invoicedate"),
             F("reference", "Reference", false, "Old invoice or ledger reference.", "invoice", "ref")]),
        new("supplier-balances", "Opening supplier balances", "Amounts owed to suppliers at cutover. Aging starts from the original invoice date.", [Permissions.FinanceManage],
            [F("supplier", "Supplier", true, "Existing active supplier name.", "name", "suppliername"), F("branch", "Branch", true, "Existing branch name."),
             F("amount", "Amount (PKR)", true, "Positive, two decimal places.", "balance", "due", "outstanding"), F("originalDate", "Original invoice date", true, "yyyy-mm-dd or dd/mm/yyyy.", "date", "invoicedate"),
             F("reference", "Reference", false, "Old invoice or ledger reference.", "invoice", "ref")]),
    ];

    private bool Can(ImportDefinition d) => scope.Has(Permissions.ImportsManage) && d.Permissions.All(scope.Has);
    private ImportDefinition Find(string type)
    {
        if (!scope.Has(Permissions.ImportsManage)) throw new AccessException(403, "Import permission required.");
        var d = Catalog.SingleOrDefault(x => x.Key == type) ?? throw new AccessException(404, "Import type not found.");
        if (!Can(d)) throw new AccessException(403, "Your role cannot import this data.");
        return d;
    }

    public ImportTypeDto[] Types()
    {
        if (!scope.Has(Permissions.ImportsManage)) throw new AccessException(403, "Import permission required.");
        return Catalog.Where(Can).Select(d => new ImportTypeDto(d.Key, d.Title, d.Description, d.Fields.Select(f => new ImportFieldDto(f.Key, f.Label, f.Required, f.Hint)).ToArray())).ToArray();
    }

    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });

    private static Dictionary<string, string?> AutoMap(ImportDefinition d, string[] headers)
    {
        var normalized = headers.ToDictionary(h => h, ImportFiles.Normalize);
        return d.Fields.ToDictionary(f => f.Key, f =>
        {
            var names = new[] { f.Key, f.Label }.Concat(f.Aliases).Select(ImportFiles.Normalize).ToHashSet();
            return headers.FirstOrDefault(h => names.Contains(normalized[h]));
        });
    }

    public async Task<ImportBatchDto> UploadAsync(string type, string fileName, Stream content, CancellationToken ct)
    {
        var d = Find(type);
        var buffer = new MemoryStream();
        var chunk = new byte[81920]; int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > ImportStatus.MaxBytes) throw new AccessException(413, "Upload files of at most 2 MB.");
        }
        var name = CatalogAccess.Text(Path.GetFileName(fileName ?? ""), 160);
        var parsed = ImportFiles.Parse(buffer.ToArray(), name);
        var batch = new ImportBatch
        {
            TenantId = scope.TenantId,
            MembershipId = scope.MembershipId,
            UserId = scope.UserId,
            Type = d.Key,
            FileName = name,
            Headers = JsonSerializer.Serialize(parsed.Headers),
            Mapping = JsonSerializer.Serialize(AutoMap(d, parsed.Headers)),
            RowCount = parsed.Rows.Count
        };
        db.ImportBatches.Add(batch);
        var rows = parsed.Rows.Select((r, i) => new ImportRow { TenantId = scope.TenantId, BatchId = batch.Id, RowNumber = i + 2, Data = JsonSerializer.Serialize(r) }).ToList();
        db.ImportRows.AddRange(rows);
        await ValidateAsync(batch, d, rows, ct);
        Audit("import.uploaded", batch.Id); await db.SaveChangesAsync(ct);
        return await DtoAsync(batch.Id, ct);
    }

    private async Task<ImportBatch> OwnAsync(Guid id, CancellationToken ct)
    {
        if (!scope.Has(Permissions.ImportsManage)) throw new AccessException(403, "Import permission required.");
        return await db.ImportBatches.SingleOrDefaultAsync(x => x.Id == id && x.MembershipId == scope.MembershipId, ct) ?? throw new AccessException(404, "Import not found.");
    }

    public async Task<ImportBatchDto> BatchAsync(Guid id, CancellationToken ct) { await OwnAsync(id, ct); return await DtoAsync(id, ct); }

    public async Task<PageDto<ImportBatchDto>> BatchesAsync(int page, CancellationToken ct)
    {
        if (!scope.Has(Permissions.ImportsManage)) throw new AccessException(403, "Import permission required.");
        if (page is < 1 or > 100_000) throw new AccessException(400, "Invalid page.");
        var query = db.ImportBatches.AsNoTracking().Where(x => x.MembershipId == scope.MembershipId);
        var ids = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * 20).Take(20).Select(x => x.Id).ToArrayAsync(ct);
        var items = new List<ImportBatchDto>(); foreach (var id in ids) items.Add(await DtoAsync(id, ct, preview: false));
        return new(items.ToArray(), await query.CountAsync(ct), page, 20);
    }

    public async Task<ImportBatchDto> MapAsync(Guid id, ImportMappingRequest request, CancellationToken ct)
    {
        var batch = await OwnAsync(id, ct); var d = Find(batch.Type);
        if (batch.Version != request.Version) throw new AccessException(409, "The import changed. Refresh first.");
        if (batch.Status is ImportStatus.Committing or ImportStatus.Committed or ImportStatus.PartiallyCommitted) throw new AccessException(409, "Mapping cannot change after committing starts.");
        var headers = JsonSerializer.Deserialize<string[]>(batch.Headers)!;
        var mapping = d.Fields.ToDictionary(f => f.Key, f => request.Mapping.GetValueOrDefault(f.Key) is { Length: > 0 } h ? h : null);
        if (request.Mapping.Keys.Any(k => d.Fields.All(f => f.Key != k))) throw new AccessException(400, "Unknown import field.");
        if (mapping.Values.Any(h => h is not null && !headers.Contains(h))) throw new AccessException(400, "Map fields to columns from the uploaded file.");
        if (mapping.Values.Where(h => h is not null).GroupBy(h => h).Any(g => g.Count() > 1)) throw new AccessException(400, "Map each column to at most one field.");
        batch.Mapping = JsonSerializer.Serialize(mapping);
        var rows = await db.ImportRows.Where(x => x.BatchId == id).OrderBy(x => x.RowNumber).ToListAsync(ct);
        await ValidateAsync(batch, d, rows, ct);
        Audit("import.validated", batch.Id); await db.SaveChangesAsync(ct);
        return await DtoAsync(id, ct);
    }

    // Lookup tables are loaded once per validation pass; rows are checked against current data and each other.
    private sealed class Lookups
    {
        public HashSet<string> Customers = [], Suppliers = [], Skus = [];
        public Dictionary<string, StockUnit> Units = [];
        public Dictionary<string, Guid> Categories = [];
        public ILookup<string, Warehouse> Warehouses = Array.Empty<Warehouse>().ToLookup(x => x.Name);
        public ILookup<string, Branch> Branches = Array.Empty<Branch>().ToLookup(x => x.Name);
        public ILookup<string, Guid> ActiveCustomers = Array.Empty<Guid>().ToLookup(x => ""), ActiveSuppliers = Array.Empty<Guid>().ToLookup(x => "");
        public Dictionary<string, (StockItem Item, StockUnit Unit)> Items = [];
        public HashSet<string> Pools = [];
        public DateOnly Today;
    }

    private static string Key(string value) => value.Trim().ToLowerInvariant();

    private async Task<Lookups> LookupsAsync(CancellationToken ct)
    {
        var branches = await BranchScope.Accessible(db, scope).Where(b => b.Active).ToArrayAsync(ct);
        var branchIds = branches.Select(b => b.Id).ToArray();
        var units = await db.StockUnits.AsNoTracking().ToArrayAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById((await db.Tenants.AsNoTracking().SingleAsync(ct)).TimeZone);
        return new Lookups
        {
            Customers = (await db.Customers.AsNoTracking().Select(x => x.Name + "|" + x.Phone).ToArrayAsync(ct)).Select(Key).ToHashSet(),
            Suppliers = (await db.Suppliers.AsNoTracking().Select(x => x.Name + "|" + x.Phone).ToArrayAsync(ct)).Select(Key).ToHashSet(),
            Skus = (await db.StockItems.AsNoTracking().Select(x => x.Sku).ToArrayAsync(ct)).Select(Key).ToHashSet(),
            Units = units.ToDictionary(u => Key(u.Code)),
            Categories = (await db.StockCategories.AsNoTracking().ToArrayAsync(ct)).GroupBy(c => Key(c.Name)).ToDictionary(g => g.Key, g => g.First().Id),
            Warehouses = (await db.Warehouses.AsNoTracking().Where(w => w.Active && branchIds.Contains(w.BranchId)).ToArrayAsync(ct)).ToLookup(w => Key(w.Name)),
            Branches = branches.ToLookup(b => Key(b.Name)),
            ActiveCustomers = (await db.Customers.AsNoTracking().Where(x => x.Active).Select(x => new { x.Name, x.Id }).ToArrayAsync(ct)).ToLookup(x => Key(x.Name), x => x.Id),
            ActiveSuppliers = (await db.Suppliers.AsNoTracking().Where(x => x.Active).Select(x => new { x.Name, x.Id }).ToArrayAsync(ct)).ToLookup(x => Key(x.Name), x => x.Id),
            Pools = (await db.StockMovements.AsNoTracking().Select(m => new { m.ItemId, m.WarehouseId, m.Bucket }).Distinct().ToArrayAsync(ct)).Select(p => $"{p.ItemId}:{p.WarehouseId}:{p.Bucket}").ToHashSet(),
            Items = (await db.StockItems.AsNoTracking().Where(i => i.Active).ToArrayAsync(ct)).ToDictionary(i => Key(i.Sku), i => (i, units.Single(u => u.Id == i.UnitId))),
            Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime)
        };
    }

    private static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    // Returns the command for a valid row, or null with errors. Duplicate keys within the file are tracked in `seen`.
    private static object? Check(ImportDefinition d, Dictionary<string, string> v, Lookups l, HashSet<string> seen, List<ImportError> errors)
    {
        string Get(string key) => v.GetValueOrDefault(key, "");
        void Error(string field, string message) => errors.Add(new(field, message));
        string Text(string key, int length) { var value = Get(key); if (value.Length > length) Error(key, $"Use at most {length} characters."); return value; }
        foreach (var f in d.Fields.Where(f => f.Required && Get(f.Key).Length == 0)) Error(f.Key, "Required.");
        decimal? Amount(string key, int places, decimal min, decimal max, bool required)
        {
            var raw = Get(key); if (raw.Length == 0) return required ? null : 0;
            var n = ImportFiles.Number(raw);
            if (n is null) { Error(key, "Enter a number."); return null; }
            if (Scale(n.Value) > places) { Error(key, $"Use at most {places} decimal places."); return null; }
            if (n < min || n > max) { Error(key, $"Enter a value from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString("N0", CultureInfo.InvariantCulture)}."); return null; }
            return n;
        }
        switch (d.Key)
        {
            case "customers" or "suppliers":
                {
                    var name = Text("name", 120); var phone = Text("phone", 80); var address = Text("address", 300);
                    var key = Key(name + "|" + phone);
                    if (name.Length > 0 && (d.Key == "customers" ? l.Customers : l.Suppliers).Contains(key)) Error("name", "Already exists with this phone.");
                    else if (name.Length > 0 && !seen.Add(key)) Error("name", "Duplicated earlier in the file.");
                    if (errors.Count > 0) return null;
                    return d.Key == "customers" ? new CustomerRequest(name, phone, address) : new SupplierRequest(name, phone, address);
                }
            case "items":
                {
                    var sku = Text("sku", 40); var name = Text("name", 120);
                    if (sku.Length > 0 && l.Skus.Contains(Key(sku))) Error("sku", "SKU already exists.");
                    else if (sku.Length > 0 && !seen.Add(Key(sku))) Error("sku", "Duplicated earlier in the file.");
                    var kind = ImportFiles.Normalize(Get("kind")) switch { "lpg" or "gas" => "Lpg", "cylinder" => "Cylinder", "sparepart" or "spare" or "part" => "SparePart", _ => null };
                    if (kind is null && Get("kind").Length > 0) Error("kind", "Use LPG, Cylinder or Spare part.");
                    l.Units.TryGetValue(Key(Get("unit")), out var unit);
                    if (unit is null && Get("unit").Length > 0) Error("unit", "Unit code not found. Create it in inventory setup first.");
                    Guid? category = null;
                    if (Get("category").Length > 0) { if (l.Categories.TryGetValue(Key(Get("category")), out var c)) category = c; else Error("category", "Category not found."); }
                    else if (kind == "SparePart") Error("category", "Spare parts require a category.");
                    var serial = ImportFiles.Flag(Get("serialTracked"));
                    if (serial is null) Error("serialTracked", "Use Yes or No.");
                    else if (serial == true && kind != "Cylinder") Error("serialTracked", "Only cylinders support serial tracking.");
                    if (kind == "Cylinder" && unit is not null && unit.DecimalPlaces != 0) Error("unit", "Cylinders need a whole-number unit.");
                    var capacity = Amount("capacityKg", 3, 0, 100000, false); var tare = Amount("tareWeightKg", 3, 0, 100000, false);
                    var reorder = Amount("reorderLevel", unit?.DecimalPlaces ?? 3, 0, 1000000, false);
                    // Mirrors the catalog rule for gas and cylinders.
                    var classification = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Get("classification").ToLowerInvariant());
                    if (kind is "Lpg" or "Cylinder" && (Get("capacityKg").Length == 0 || capacity <= 0)) Error("capacityKg", "LPG and cylinders need a positive capacity.");
                    if (kind is "Lpg" or "Cylinder" && classification is not ("Domestic" or "Commercial")) Error("classification", "Use Domestic or Commercial.");
                    if (errors.Count > 0) return null;
                    return new ItemRequest(sku, name, kind!, unit!.Id, category, Text("brand", 80), classification,
                        Get("capacityKg").Length > 0 ? capacity : null, Get("tareWeightKg").Length > 0 ? tare : null, serial == true, reorder ?? 0);
                }
            case "opening-stock":
                {
                    (StockItem Item, StockUnit Unit) item = default; var hasItem = Get("sku").Length > 0 && l.Items.TryGetValue(Key(Get("sku")), out item);
                    if (!hasItem && Get("sku").Length > 0) Error("sku", "Active item not found. Import items first.");
                    var warehouses = l.Warehouses[Key(Get("warehouse"))].ToArray();
                    if (Get("warehouse").Length > 0 && warehouses.Length != 1) Error("warehouse", warehouses.Length == 0 ? "Active warehouse not found in your branches." : "Warehouse name is not unique; rename one first.");
                    var cylinder = hasItem && item.Item!.Kind == "Cylinder";
                    var state = Get("state").Length == 0 ? (cylinder ? "Full" : "Available") : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Get("state").ToLowerInvariant());
                    var allowed = cylinder ? new[] { "Full", "Empty", "Inspection", "Damaged" } : ["Available", "Damaged"];
                    if (hasItem && !allowed.Contains(state)) Error("state", "Use " + string.Join(", ", allowed) + ".");
                    // Mirrors the stock rule: an opening can be posted once per item, warehouse and state.
                    if (hasItem && warehouses.Length == 1 && allowed.Contains(state))
                    {
                        var pool = $"{item.Item!.Id}:{warehouses[0].Id}:{state}";
                        if (l.Pools.Contains(pool)) Error("state", "Opening stock already exists for this state; use an adjustment.");
                        else if (!seen.Add("pool:" + pool)) Error("state", "Duplicated earlier in the file.");
                    }
                    var quantity = Amount("quantity", hasItem ? item.Unit!.DecimalPlaces : 3, 0.001m, 1000000, true);
                    if (quantity == 0) Error("quantity", "Quantity must be positive.");
                    var cost = Amount("unitCost", 6, 0, 1000000000, true);
                    var serials = Get("serials").Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToUpperInvariant()).ToArray();
                    if (hasItem && item.Item!.SerialTracked && quantity.HasValue && serials.Length != quantity) Error("serials", "Give one serial number per cylinder.");
                    if (hasItem && !item.Item!.SerialTracked && serials.Length > 0) Error("serials", "This item is not serial tracked.");
                    foreach (var s in serials.Where(s => !seen.Add("serial:" + s))) Error("serials", $"Serial {s} appears twice in the file.");
                    if (errors.Count > 0) return null;
                    return (item.Item!.Id, warehouses[0].Id, state, quantity!.Value, cost!.Value, serials);
                }
            default:
                {
                    var supplier = d.Key == "supplier-balances"; var party = Get(supplier ? "supplier" : "customer");
                    var parties = (supplier ? l.ActiveSuppliers : l.ActiveCustomers)[Key(party)].ToArray();
                    if (party.Length > 0 && parties.Length != 1) Error(supplier ? "supplier" : "customer", parties.Length == 0 ? "Active record not found. Import it first." : "Name matches several records; rename duplicates first.");
                    var branches = l.Branches[Key(Get("branch"))].ToArray();
                    if (Get("branch").Length > 0 && branches.Length != 1) Error("branch", "Active branch not found in your access.");
                    var amount = Amount("amount", 2, 0.01m, 1000000000, true);
                    var date = Get("originalDate").Length == 0 ? null : ImportFiles.Date(Get("originalDate"));
                    if (Get("originalDate").Length > 0 && (date is null || date > l.Today || date < new DateOnly(2000, 1, 1))) Error("originalDate", "Enter a past date as yyyy-mm-dd or dd/mm/yyyy.");
                    var reference = Text("reference", 120);
                    if (errors.Count > 0) return null;
                    return new OpeningBalanceRequest(branches[0].Id, supplier ? null : parties[0], supplier ? parties[0] : null, reference, date!.Value, amount!.Value, "Imported opening balance", "");
                }
        }
    }

    private static Dictionary<string, string> Values(ImportDefinition d, string[] headers, Dictionary<string, string?> mapping, string data)
    {
        var raw = JsonSerializer.Deserialize<string[]>(data)!;
        return d.Fields.ToDictionary(f => f.Key, f => mapping.GetValueOrDefault(f.Key) is { } h && Array.IndexOf(headers, h) is >= 0 and var i && i < raw.Length ? raw[i] : "");
    }

    private async Task<Dictionary<int, object>> ValidateAsync(ImportBatch batch, ImportDefinition d, List<ImportRow> rows, CancellationToken ct)
    {
        var headers = JsonSerializer.Deserialize<string[]>(batch.Headers)!; var mapping = JsonSerializer.Deserialize<Dictionary<string, string?>>(batch.Mapping)!;
        var lookups = await LookupsAsync(ct); var seen = new HashSet<string>(); var commands = new Dictionary<int, object>();
        var missing = d.Fields.Where(f => f.Required && mapping.GetValueOrDefault(f.Key) is null).Select(f => f.Label).ToArray();
        foreach (var row in rows)
        {
            if (row.Status == ImportStatus.Committed) continue;
            var errors = new List<ImportError>();
            if (missing.Length > 0) errors.Add(new("mapping", "Map the required fields: " + string.Join(", ", missing) + "."));
            else if (Check(d, Values(d, headers, mapping, row.Data), lookups, seen, errors) is { } command) commands[row.RowNumber] = command;
            row.Errors = JsonSerializer.Serialize(errors); row.Status = errors.Count == 0 ? ImportStatus.Valid : ImportStatus.Invalid;
        }
        batch.ErrorCount = rows.Count(r => r.Status == ImportStatus.Invalid);
        batch.Status = batch.ErrorCount > 0 ? ImportStatus.Invalid : rows.Any(r => r.Status == ImportStatus.Committed) ? ImportStatus.PartiallyCommitted : ImportStatus.Validated;
        return commands;
    }

    public async Task<ImportBatchDto> CommitAsync(Guid id, ImportCommitRequest request, CancellationToken ct)
    {
        var batch = await OwnAsync(id, ct); var d = Find(batch.Type);
        if (batch.Status == ImportStatus.Committed) return await DtoAsync(id, ct);
        if (batch.Version != request.Version) throw new AccessException(409, "The import changed. Refresh first.");
        if (batch.Status == ImportStatus.Invalid) throw new AccessException(409, "Fix or remove the rows with errors, then upload again.");
        // Claim the batch so two commits cannot interleave; a commit abandoned for ten minutes may be resumed.
        var stale = DateTimeOffset.UtcNow.AddMinutes(-10);
        var claimed = await db.ImportBatches.Where(x => x.Id == id && x.Version == request.Version &&
                (x.Status == ImportStatus.Validated || x.Status == ImportStatus.PartiallyCommitted || x.Status == ImportStatus.Committing && x.CommittedAt < stale))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ImportStatus.Committing).SetProperty(x => x.CommittedAt, DateTimeOffset.UtcNow), ct);
        if (claimed == 0) throw new AccessException(409, "This import is already being committed.");
        db.ChangeTracker.Clear();
        batch = await db.ImportBatches.SingleAsync(x => x.Id == id, ct);
        var rows = await db.ImportRows.Where(x => x.BatchId == id).OrderBy(x => x.RowNumber).ToListAsync(ct);
        // Data may have changed since preview; revalidate before writing anything.
        var commands = await ValidateAsync(batch, d, rows, ct);
        if (batch.Status == ImportStatus.Invalid) { await db.SaveChangesAsync(ct); throw new AccessException(409, "Data changed since validation. Review the errors before committing."); }
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        foreach (var (rowNumber, command) in commands.OrderBy(x => x.Key))
        {
            var key = $"import:{id:N}:{rowNumber}";
            try
            {
                var result = await ExecuteAsync(d.Key, command, key, ct);
                await db.ImportRows.Where(x => x.BatchId == id && x.RowNumber == rowNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ImportStatus.Committed).SetProperty(x => x.ResultId, result).SetProperty(x => x.Errors, "[]"), ct);
            }
            catch (Exception exception) when (exception is AccessException or DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var message = exception is AccessException access ? access.Message : "The row conflicts with existing data.";
                var errors = JsonSerializer.Serialize(new[] { new ImportError("row", message) });
                await db.ImportRows.Where(x => x.BatchId == id && x.RowNumber == rowNumber)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ImportStatus.Failed).SetProperty(x => x.Errors, errors), ct);
            }
        }
        var committed = await db.ImportRows.CountAsync(x => x.BatchId == id && x.Status == ImportStatus.Committed, ct);
        var failed = await db.ImportRows.CountAsync(x => x.BatchId == id && x.Status == ImportStatus.Failed, ct);
        await db.ImportBatches.Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, committed == batch.RowCount ? ImportStatus.Committed : ImportStatus.PartiallyCommitted)
            .SetProperty(x => x.CommittedCount, committed).SetProperty(x => x.ErrorCount, failed).SetProperty(x => x.CommittedAt, DateTimeOffset.UtcNow), ct);
        Audit("import.committed", id); await db.SaveChangesAsync(ct);
        return await DtoAsync(id, ct);
    }

    // Master data commits in one transaction with its row status. Ledger commands open their own transaction and
    // are idempotent by the row's deterministic key, so a resumed commit replays instead of duplicating.
    private async Task<Guid> ExecuteAsync(string type, object command, string key, CancellationToken ct)
    {
        switch (type)
        {
            case "customers": return (await new CustomerAccess(db, scope).SaveCustomerAsync(null, (CustomerRequest)command, ct)).Id;
            case "suppliers": return (await new PurchasingAccess(db, scope, costing).SaveSupplierAsync(null, (SupplierRequest)command, ct)).Id;
            case "items": return (await new CatalogAccess(db, scope).SaveItemAsync(null, (ItemRequest)command, ct)).Id;
            case "opening-stock":
                {
                    var (item, warehouse, state, quantity, cost, serials) = ((Guid, Guid, string, decimal, decimal, string[]))command;
                    var sequence = await db.StockMovements.Where(x => x.ItemId == item && x.WarehouseId == warehouse).MaxAsync(x => (long?)x.Sequence, ct) ?? 0;
                    var request = new PostStockRequest(key, "Opening", item, warehouse, state, quantity, cost, null, null, "Imported opening stock", sequence, 0, serials);
                    return (await new InventoryAccess(db, scope, costing).PostAsync(request, ct)).Id;
                }
            default: return (await new FinanceAccess(db, scope).CreateOpeningAsync((OpeningBalanceRequest)command with { IdempotencyKey = key }, ct)).Id;
        }
    }

    public async Task<ExportFile> ErrorsAsync(Guid id, CancellationToken ct)
    {
        var batch = await OwnAsync(id, ct); var d = Find(batch.Type);
        var rows = await db.ImportRows.AsNoTracking().Where(x => x.BatchId == id && (x.Status == ImportStatus.Invalid || x.Status == ImportStatus.Failed)).OrderBy(x => x.RowNumber).ToArrayAsync(ct);
        var labels = d.Fields.ToDictionary(f => f.Key, f => f.Label);
        var csv = new StringBuilder("Row,Field,Error\r\n");
        foreach (var row in rows)
            foreach (var e in JsonSerializer.Deserialize<ImportError[]>(row.Errors)!)
                csv.Append(row.RowNumber).Append(',').Append(ReportFiles.CsvCell(labels.GetValueOrDefault(e.Field, e.Field))).Append(',').Append(ReportFiles.CsvCell(e.Message)).Append("\r\n");
        return new($"{Path.GetFileNameWithoutExtension(batch.FileName)}-errors.csv", "text/csv; charset=utf-8", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())]);
    }

    private async Task<ImportBatchDto> DtoAsync(Guid id, CancellationToken ct, bool preview = true)
    {
        var batch = await db.ImportBatches.AsNoTracking().SingleAsync(x => x.Id == id, ct); var d = Catalog.Single(x => x.Key == batch.Type);
        var headers = JsonSerializer.Deserialize<string[]>(batch.Headers)!; var mapping = JsonSerializer.Deserialize<Dictionary<string, string?>>(batch.Mapping)!;
        // Preview shows problems first, then the first rows, so operators see what needs fixing.
        var rows = preview ? await db.ImportRows.AsNoTracking().Where(x => x.BatchId == id)
            .OrderBy(x => x.Status == ImportStatus.Invalid || x.Status == ImportStatus.Failed ? 0 : 1).ThenBy(x => x.RowNumber).Take(20).ToArrayAsync(ct) : [];
        return new(batch.Id, batch.Type, batch.FileName, batch.Status, headers, mapping, batch.RowCount, batch.ErrorCount, batch.CommittedCount, batch.CreatedAt, batch.CommittedAt, batch.Version,
            rows.Select(r => new ImportPreviewRowDto(r.RowNumber, r.Status, Values(d, headers, mapping, r.Data),
                JsonSerializer.Deserialize<ImportError[]>(r.Errors)!.Select(e => $"{d.Fields.FirstOrDefault(f => f.Key == e.Field)?.Label ?? e.Field}: {e.Message}").ToArray())).ToArray());
    }
}
