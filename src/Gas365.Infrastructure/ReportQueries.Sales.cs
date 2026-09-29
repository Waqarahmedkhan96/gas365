using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class ReportAccess
{
    private sealed record ProfitLine(DateOnly Date, Guid BranchId, decimal LpgRevenue, decimal LpgCost, decimal SpareRevenue, decimal SpareCost);

    private async Task<(ReportColumn[], List<object?[]>, object?[])> DailySalesAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var groups = await db.SaleEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= f.From && x.BusinessDate <= f.To)
            .GroupBy(x => new { x.BusinessDate, x.BranchId })
            .Select(g => new
            {
                g.Key.BusinessDate,
                g.Key.BranchId,
                Invoices = g.Count(x => x.Kind == "Invoice"),
                Gross = g.Where(x => x.Kind == "Invoice").Sum(x => x.Revenue),
                Reversed = g.Where(x => x.Kind == "Reversal").Sum(x => x.Revenue),
                Net = g.Sum(x => x.Revenue),
                Cash = g.Where(x => x.Kind == "Payment" || x.Kind == "PaymentReversal").Sum(x => x.Cash),
                Advances = -g.Where(x => x.Kind == "Allocation" || x.Kind == "AllocationReversal").Sum(x => x.Receivable),
                Receivable = g.Sum(x => x.Receivable)
            }).ToArrayAsync(ct);
        ReportColumn[] columns = [new("date", "Date", "date"), Text("branch", "Branch"), new("invoices", "Invoices", "count"), Money("gross", "Invoiced revenue"),
            Money("reversed", "Reversed revenue"), Money("net", "Net revenue"), Money("cash", "Cash received"), Money("advances", "Advances applied"), Money("receivable", "Receivable change")];
        var rows = groups.OrderBy(x => x.BusinessDate).ThenBy(x => names[x.BranchId])
            .Select(x => new object?[] { Date(x.BusinessDate), names[x.BranchId], x.Invoices, x.Gross, x.Reversed, x.Net, x.Cash, x.Advances, x.Receivable }).ToList();
        return (columns, rows, Sum(columns, rows, "invoices", "gross", "reversed", "net", "cash", "advances", "receivable"));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> PurchasesAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var entries = await (from e in db.PurchaseEntries.AsNoTracking()
                             join d in db.PurchaseDocuments.AsNoTracking() on e.PurchaseId equals d.Id
                             where ids.Contains(e.BranchId) && e.BusinessDate >= f.From && e.BusinessDate <= f.To
                             select new { e.BusinessDate, e.BranchId, d.SupplierName, e.Kind, e.Payable, e.Cash }).ToArrayAsync(ct);
        ReportColumn[] columns = [new("date", "Date", "date"), Text("branch", "Branch"), Text("supplier", "Supplier"), new("invoices", "Invoices", "count"),
            Money("invoiced", "Invoiced"), Money("reversed", "Reversed"), Money("net", "Net purchases"), Money("paid", "Paid to supplier"),
            Money("costs", "Separate costs paid"), Money("advances", "Advances applied"), Money("payable", "Payable change")];
        var rows = entries.GroupBy(x => new { x.BusinessDate, x.BranchId, x.SupplierName }).OrderBy(g => g.Key.BusinessDate).ThenBy(g => names[g.Key.BranchId]).ThenBy(g => g.Key.SupplierName)
            .Select(g => new object?[]
            {
                Date(g.Key.BusinessDate), names[g.Key.BranchId], g.Key.SupplierName, g.Count(x => x.Kind == "Invoice"),
                g.Where(x => x.Kind == "Invoice").Sum(x => x.Payable), g.Where(x => x.Kind == "Reversal").Sum(x => x.Payable),
                g.Where(x => x.Kind is "Invoice" or "Reversal").Sum(x => x.Payable),
                -g.Where(x => x.Kind is "Payment" or "PaymentReversal").Sum(x => x.Cash),
                -g.Where(x => x.Kind is "CostPayment" or "CostPaymentReversal").Sum(x => x.Cash),
                -g.Where(x => x.Kind is "Allocation" or "AllocationReversal").Sum(x => x.Payable),
                g.Sum(x => x.Payable)
            }).ToList();
        return (columns, rows, Sum(columns, rows, "invoices", "invoiced", "reversed", "net", "paid", "costs", "advances", "payable"));
    }

    // Splits each invoice or reversal entry exactly: spare parts come from the frozen line snapshot and the
    // sale's stock movements; LPG is the journal amount minus spare parts, so categories always sum to the journal.
    private async Task<List<ProfitLine>> ProfitLinesAsync(Guid[] ids, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var entries = await db.SaleEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to && (x.Kind == "Invoice" || x.Kind == "Reversal"))
            .Select(x => new { x.SaleId, x.Kind, x.BranchId, x.BusinessDate, x.Revenue, x.Cogs }).ToArrayAsync(ct);
        var saleIds = entries.Select(x => x.SaleId).Distinct().ToArray();
        var docs = await db.SaleDocuments.AsNoTracking().Where(x => saleIds.Contains(x.Id)).Select(x => new { x.Id, x.Content, x.StockDocumentId }).ToDictionaryAsync(x => x.Id, ct);
        var stockIds = docs.Values.Select(x => x.StockDocumentId).ToArray();
        var spareCost = await (from m in db.StockMovements.AsNoTracking()
                               join i in db.StockItems.AsNoTracking() on m.ItemId equals i.Id
                               where stockIds.Contains(m.DocumentId) && i.Kind == "SparePart"
                               group m by m.DocumentId into g
                               select new { DocumentId = g.Key, Cost = -g.Sum(x => x.Value) }).ToDictionaryAsync(x => x.DocumentId, x => x.Cost, ct);
        var spareRevenue = docs.Values.ToDictionary(x => x.Id, x => JsonSerializer.Deserialize<SaleDraftSnapshot>(x.Content)!.Lines
            .Where(l => ReportCalculation.SaleCategory(l.Mode) == ReportCalculation.SpareParts).Sum(l => l.Net));
        return entries.Select(e =>
        {
            var sign = e.Kind == "Invoice" ? 1 : -1; var doc = docs[e.SaleId];
            var revenue = sign * spareRevenue[e.SaleId]; var cost = sign * spareCost.GetValueOrDefault(doc.StockDocumentId);
            return new ProfitLine(e.BusinessDate, e.BranchId, e.Revenue - revenue, e.Cogs - cost, revenue, cost);
        }).ToList();
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> CategoryProfitAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, string category, CancellationToken ct)
    {
        var lines = await ProfitLinesAsync(ids, f.From, f.To, ct); var lpg = category == ReportCalculation.Lpg;
        ReportColumn[] columns = [new("date", "Date", "date"), Text("branch", "Branch"), Money("revenue", "Revenue"), Money("cost", "Cost of goods sold"), Money("profit", "Gross profit"), new("margin", "Margin %", "percent")];
        var rows = lines.GroupBy(x => new { x.Date, x.BranchId })
            .Select(g => new { g.Key.Date, g.Key.BranchId, Revenue = g.Sum(x => lpg ? x.LpgRevenue : x.SpareRevenue), Cost = g.Sum(x => lpg ? x.LpgCost : x.SpareCost) })
            .Where(x => x.Revenue != 0 || x.Cost != 0).OrderBy(x => x.Date).ThenBy(x => names[x.BranchId])
            .Select(x => new object?[] { Date(x.Date), names[x.BranchId], x.Revenue, x.Cost, x.Revenue - x.Cost, ReportCalculation.Margin(x.Revenue, x.Revenue - x.Cost) }).ToList();
        var totals = Sum(columns, rows, "revenue", "cost", "profit");
        totals[5] = ReportCalculation.Margin((decimal)totals[2]!, (decimal)totals[4]!);
        return (columns, rows, totals);
    }

    private Task<decimal> CylinderChargesAsync(Guid[] ids, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.CylinderLoanEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to).SumAsync(x => x.Revenue, ct);

    private async Task<(decimal Operating, decimal Capital)> ExpenseTotalsAsync(Guid[] ids, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await (from e in db.FinanceEntries.AsNoTracking()
                          join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
                          where ids.Contains(e.BranchId) && e.BusinessDate >= @from && e.BusinessDate <= to && (e.Kind == "Expense" || e.Kind == "ExpenseReversal")
                          group e by c.Capital into g
                          select new { Capital = g.Key, Amount = -g.Sum(x => x.Cash) }).ToArrayAsync(ct);
        return (rows.Where(x => !x.Capital).Sum(x => x.Amount), rows.Where(x => x.Capital).Sum(x => x.Amount));
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> ProfitAsync(Guid[] ids, ReportFilter f, CancellationToken ct)
    {
        var lines = await ProfitLinesAsync(ids, f.From, f.To, ct);
        var charges = await CylinderChargesAsync(ids, f.From, f.To, ct); var (operating, capital) = await ExpenseTotalsAsync(ids, f.From, f.To, ct);
        decimal lpgRevenue = lines.Sum(x => x.LpgRevenue), spareRevenue = lines.Sum(x => x.SpareRevenue), lpgCost = lines.Sum(x => x.LpgCost), spareCost = lines.Sum(x => x.SpareCost);
        var revenue = lpgRevenue + spareRevenue + charges; var gross = revenue - lpgCost - spareCost;
        ReportColumn[] columns = [Text("line", "Line"), Money("amount", "Amount")];
        var rows = new List<object?[]>
        {
            new object?[] { "LPG and cylinder sales", lpgRevenue }, new object?[] { "Spare-part sales", spareRevenue },
            new object?[] { "Cylinder usage and damage charges", charges }, new object?[] { "Total revenue", revenue },
            new object?[] { "Cost of goods sold: LPG and cylinders", lpgCost }, new object?[] { "Cost of goods sold: spare parts", spareCost },
            new object?[] { "Gross profit", gross }, new object?[] { "Operating expenses", operating }, new object?[] { "Net profit", gross - operating },
            new object?[] { "Memo: capital expenses (excluded)", capital }
        };
        return (columns, rows, [null, null]);
    }

    private async Task<(ReportColumn[], List<object?[]>, object?[])> BranchComparisonAsync(Guid[] ids, Dictionary<Guid, string> names, ReportFilter f, CancellationToken ct)
    {
        var sales = await db.SaleEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= f.From && x.BusinessDate <= f.To)
            .GroupBy(x => x.BranchId).Select(g => new { Branch = g.Key, Revenue = g.Sum(x => x.Revenue), Cost = g.Sum(x => x.Cogs), Cash = g.Where(x => x.Kind == "Payment" || x.Kind == "PaymentReversal").Sum(x => x.Cash) })
            .ToDictionaryAsync(x => x.Branch, ct);
        var charges = await db.CylinderLoanEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= f.From && x.BusinessDate <= f.To)
            .GroupBy(x => x.BranchId).Select(g => new { Branch = g.Key, Revenue = g.Sum(x => x.Revenue) }).ToDictionaryAsync(x => x.Branch, x => x.Revenue, ct);
        var purchases = await db.PurchaseEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= f.From && x.BusinessDate <= f.To && (x.Kind == "Invoice" || x.Kind == "Reversal"))
            .GroupBy(x => x.BranchId).Select(g => new { Branch = g.Key, Amount = g.Sum(x => x.Payable) }).ToDictionaryAsync(x => x.Branch, x => x.Amount, ct);
        var expenses = await (from e in db.FinanceEntries.AsNoTracking()
                              join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
                              where ids.Contains(e.BranchId) && e.BusinessDate >= f.From && e.BusinessDate <= f.To && (e.Kind == "Expense" || e.Kind == "ExpenseReversal") && !c.Capital
                              group e by e.BranchId into g
                              select new { Branch = g.Key, Amount = -g.Sum(x => x.Cash) }).ToDictionaryAsync(x => x.Branch, x => x.Amount, ct);
        ReportColumn[] columns = [Text("branch", "Branch"), Money("revenue", "Net revenue"), Money("cost", "Cost of goods sold"), Money("charges", "Cylinder charges"),
            Money("gross", "Gross profit"), Money("purchases", "Net purchases"), Money("expenses", "Operating expenses"), Money("net", "Net profit"), Money("cash", "Sale cash received")];
        var rows = ids.OrderBy(id => names[id]).Select(id =>
        {
            var s = sales.GetValueOrDefault(id); var revenue = s?.Revenue ?? 0; var cost = s?.Cost ?? 0; var fee = charges.GetValueOrDefault(id);
            var gross = revenue - cost + fee; var expense = expenses.GetValueOrDefault(id);
            return new object?[] { names[id], revenue, cost, fee, gross, purchases.GetValueOrDefault(id), expense, gross - expense, s?.Cash ?? 0 };
        }).ToList();
        return (columns, rows, Sum(columns, rows, "revenue", "cost", "charges", "gross", "purchases", "expenses", "net", "cash"));
    }
}
