using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

internal sealed record ReportDefinition(string Key, string Title, string Definition, string Reconciliation, string[] Permissions, string[] Types, bool DateRange, bool BranchScoped);
internal sealed record ReportData(ReportDefinition Definition, ReportFilter Filter, string Scope, ReportColumn[] Columns, List<object?[]> Rows, object?[] Totals);

public sealed partial class ReportAccess(Gas365DbContext db, TenantScope scope) : IReportAccess
{
    private const int PageSize = 100;
    internal const int MaxRows = 100_000;
    private const int MaxDays = 366;
    private static readonly string[] ItemKinds = ["Lpg", "Cylinder", "SparePart"];
    // Audit actions are "<prefix>.<verb>"; areas group prefixes the way owners think about the business.
    internal static readonly Dictionary<string, string[]> ActivityAreas = new()
    {
        ["access"] = ["login", "session", "password", "email", "member", "role", "branch", "tenant"],
        ["inventory"] = ["stock", "item", "unit", "category", "warehouse", "asset"],
        ["purchasing"] = ["purchase", "supplier"],
        ["sales"] = ["sale", "customer", "price_list", "cylinder"],
        ["finance"] = ["finance", "register"],
        ["reports"] = ["report"],
    };

    internal static readonly ReportDefinition[] Catalog =
    [
        new("daily-sales", "Daily sales", "Posted sale journal activity per business date and branch: invoices, reversals, net revenue, cash received, advances applied and the net change in customer receivables.",
            "Net revenue equals the sum of sale journal revenue for the period; receivable change equals the sum of sale journal receivable movements.", [Permissions.SalesView], [], true, true),
        new("purchases", "Purchases", "Posted supplier invoices per business date, branch and supplier, with reversals, cash paid to the supplier, separately paid purchase costs and advances applied.",
            "Net purchases equal invoice plus reversal payable in the purchase journal; payable change equals all purchase journal payable movements.", [Permissions.PurchaseView], [], true, true),
        new("stock-on-hand", "Stock on hand", "Quantity and value per warehouse, item and stock state at the end of the as-of date, derived from the immutable stock ledger.",
            "Total value equals the sum of stock movement values dated on or before the as-of date.", [Permissions.InventoryView], ItemKinds, false, true),
        new("stock-movement", "Stock movement", "Opening, received, issued and closing quantity and value per warehouse and item. Movements within one document between stock states net off, so internal state changes are not counted twice.",
            "Opening plus received minus issued equals closing for every row; closing value equals stock on hand at the end date.", [Permissions.InventoryView], ItemKinds, true, true),
        new("inventory-valuation", "Inventory valuation", "Weighted-average valuation per item across the selected warehouses at the end of the as-of date, including customer-held business cylinders.",
            "Total value equals the stock-on-hand total for the same filters.", [Permissions.InventoryView], ItemKinds, false, true),
        new("lpg-profit", "LPG profit", "Revenue, cost of goods sold and gross profit for gas and cylinder sale lines per business date and branch. Cost includes recorded filling loss.",
            "LPG plus spare-part revenue and cost equal the sale journal revenue and cost for the period.", [Permissions.SalesProfit], [], true, true),
        new("spare-parts-profit", "Spare-parts profit", "Revenue, cost of goods sold and gross profit for spare-part sale lines per business date and branch.",
            "LPG plus spare-part revenue and cost equal the sale journal revenue and cost for the period.", [Permissions.SalesProfit], [], true, true),
        new("expenses", "Expenses", "Posted expenses net of reversals per category and branch. Capital items are classified separately and excluded from operating profit.",
            "Total equals the cash paid by expense entries and their reversals for the period.", [Permissions.FinanceView], ["Operating", "Capital"], true, true),
        new("profit", "Gross and net profit", "Profit statement: sales revenue by category, cylinder usage and damage charges, cost of goods sold, gross profit, operating expenses and net profit. Capital expenses are shown as a memo line only.",
            "Revenue and cost tie to the sale journal, charges to the cylinder journal and operating expenses to the expenses report.", [Permissions.SalesProfit, Permissions.FinanceView], [], true, true),
        new("customer-aging", "Customer aging", "Outstanding customer invoices and cylinder settlements at the as-of date, aged from the invoice business date, with unapplied advances.",
            "Total due equals sale and cylinder journal receivables on or before the as-of date; unapplied equals customer finance entries.", [Permissions.FinanceView], [], false, true),
        new("supplier-payables", "Supplier payables", "Outstanding supplier invoices at the as-of date, aged from the invoice business date, with unapplied supplier advances.",
            "Total due equals purchase journal payables on or before the as-of date; unapplied equals supplier finance entries.", [Permissions.FinanceView, Permissions.PurchaseView], [], false, true),
        new("cash-reconciliation", "Cash reconciliation", "Daily account reconciliation: opening balance, every formula line of the cash rule, expected close, counted cash, variance and register status.",
            "Opening plus all category lines equals expected close, which matches the cash register for the same account and date.", [Permissions.FinanceView], ["Cash", "Bank"], true, true),
        new("cylinder-custody", "Cylinder custody", "Business-owned cylinders held by customers at the as-of date, with refundable deposits held and settlement amounts due.",
            "Cylinders on loan equal customer-held stock in the inventory ledger.", [Permissions.SalesView], [], false, true),
        new("branch-comparison", "Branch comparison", "Revenue, cost, cylinder charges, gross profit, purchases, operating expenses, net profit and sale cash received per branch.",
            "Branch rows sum to the profit statement, purchases and expenses reports for the same period.", [Permissions.SalesProfit, Permissions.FinanceView, Permissions.PurchaseView], [], true, true),
        new("user-activity", "User activity", "Audited actions per user and action type for the period, in the tenant time zone. Workspace-wide; the branch filter does not apply.",
            "The action count equals audit events in the period.", [Permissions.AuditView], [.. ActivityAreas.Keys], true, false),
    ];

    private bool Can(ReportDefinition d) => scope.Has(Permissions.ReportsView) && d.Permissions.All(scope.Has);
    private void Require(string permission) { if (!scope.Has(permission)) throw new AccessException(403, "Report permission required."); }

    public ReportDefinitionDto[] Definitions()
    {
        Require(Permissions.ReportsView);
        return Catalog.Where(Can).Select(d => new ReportDefinitionDto(d.Key, d.Title, d.Definition, d.Reconciliation, d.Types, d.DateRange, d.BranchScoped)).ToArray();
    }

    internal ReportDefinition Find(string key)
    {
        Require(Permissions.ReportsView);
        var d = Catalog.SingleOrDefault(x => x.Key == key) ?? throw new AccessException(404, "Report not found.");
        if (!Can(d)) throw new AccessException(403, "Your role cannot view this report.");
        return d;
    }

    internal static ReportFilter Normalize(ReportDefinition d, ReportFilter f)
    {
        var from = d.DateRange ? f.From : f.To;
        if (from > f.To) throw new AccessException(400, "The start date must be on or before the end date.");
        if (f.To.DayNumber - from.DayNumber >= MaxDays) throw new AccessException(400, "Choose a period of at most 366 days.");
        var type = string.IsNullOrWhiteSpace(f.Type) ? null : f.Type;
        if (type is not null && !d.Types.Contains(type, StringComparer.Ordinal)) throw new AccessException(400, "Unknown report type filter.");
        return new(d.BranchScoped ? f.BranchId : null, from, f.To, type);
    }

    // Reports include inactive branches so history stays visible; access follows the member's branch grants.
    private async Task<(Guid[] Ids, Dictionary<Guid, string> Names, string Scope)> BranchesAsync(Guid? branchId, CancellationToken ct)
    {
        var query = db.Branches.AsNoTracking();
        if (!(scope.IsOwner || scope.Has(Permissions.BranchManage)))
            query = query.Where(b => db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == b.Id));
        if (branchId.HasValue) query = query.Where(b => b.Id == branchId);
        var rows = await query.OrderBy(b => b.Name).Select(b => new { b.Id, b.Name }).ToArrayAsync(ct);
        if (branchId.HasValue && rows.Length == 0) throw new AccessException(404, "Branch not found.");
        return (rows.Select(x => x.Id).ToArray(), rows.ToDictionary(x => x.Id, x => x.Name), branchId.HasValue ? rows[0].Name : "All accessible branches");
    }

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct) => TimeZoneInfo.FindSystemTimeZoneById((await db.Tenants.AsNoTracking().SingleAsync(ct)).TimeZone);

    internal async Task<ReportData> BuildAsync(string key, ReportFilter filter, CancellationToken ct)
    {
        var d = Find(key); var f = Normalize(d, filter);
        var (ids, names, label) = await BranchesAsync(f.BranchId, ct);
        var (columns, rows, totals) = key switch
        {
            "daily-sales" => await DailySalesAsync(ids, names, f, ct),
            "purchases" => await PurchasesAsync(ids, names, f, ct),
            "stock-on-hand" => await StockOnHandAsync(ids, names, f, ct),
            "stock-movement" => await StockMovementAsync(ids, names, f, ct),
            "inventory-valuation" => await ValuationAsync(ids, f, ct),
            "lpg-profit" => await CategoryProfitAsync(ids, names, f, ReportCalculation.Lpg, ct),
            "spare-parts-profit" => await CategoryProfitAsync(ids, names, f, ReportCalculation.SpareParts, ct),
            "expenses" => await ExpensesAsync(ids, names, f, ct),
            "profit" => await ProfitAsync(ids, f, ct),
            "customer-aging" => await AgingAsync(ids, f, supplier: false, ct),
            "supplier-payables" => await AgingAsync(ids, f, supplier: true, ct),
            "cash-reconciliation" => await CashAsync(ids, names, f, ct),
            "cylinder-custody" => await CustodyAsync(ids, f, ct),
            "branch-comparison" => await BranchComparisonAsync(ids, names, f, ct),
            "user-activity" => await ActivityAsync(f, ct),
            _ => throw new AccessException(404, "Report not found.")
        };
        if (rows.Count > MaxRows) throw new AccessException(413, "This report has too many rows. Narrow the dates or branch.");
        return new(d, f, d.BranchScoped ? label : "Workspace", columns, rows, totals);
    }

    public async Task<ReportDto> RunAsync(string key, ReportFilter filter, int page, CancellationToken ct)
    {
        if (page is < 1 or > 100_000) throw new AccessException(400, "Invalid page.");
        var data = await BuildAsync(key, filter, ct);
        return new(data.Definition.Key, data.Definition.Title, data.Definition.Definition, data.Filter, data.Scope, data.Columns,
            data.Rows.Skip((page - 1) * PageSize).Take(PageSize).ToArray(), data.Totals, data.Rows.Count, page, PageSize, DateTimeOffset.UtcNow);
    }

    private void Audit(string action, Guid? subject) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = subject });
    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    // Totals for additive columns only; text, date and ratio columns stay null.
    private static object?[] Sum(ReportColumn[] columns, List<object?[]> rows, params string[] additive) =>
        columns.Select((c, i) => additive.Contains(c.Key) ? (object?)(c.Type == "count" ? rows.Sum(r => Convert.ToInt32(r[i] ?? 0)) : rows.Sum(r => (decimal?)r[i] ?? 0)) : null).ToArray();
    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static ReportColumn Text(string key, string label) => new(key, label, "text");
    private static ReportColumn Money(string key, string label) => new(key, label, "money");
}
