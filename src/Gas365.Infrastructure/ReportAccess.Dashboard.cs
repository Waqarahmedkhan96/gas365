using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class ReportAccess
{
    // Each metric appears only when the member holds the permission its underlying report needs.
    public async Task<DashboardDto> DashboardAsync(Guid? branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        Require(Permissions.ReportsView);
        if (from > to || to.DayNumber - from.DayNumber >= MaxDays) throw new AccessException(400, "Choose a period of at most 366 days.");
        var (ids, names, label) = await BranchesAsync(branchId, ct);
        bool sales = scope.Has(Permissions.SalesView), profit = sales && scope.Has(Permissions.SalesProfit), finance = scope.Has(Permissions.FinanceView);
        bool purchases = scope.Has(Permissions.PurchaseView), inventory = scope.Has(Permissions.InventoryView);
        var metrics = new List<DashboardMetricDto>();
        var trend = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).ToDictionary(d => d, _ => (Revenue: 0m, Cost: 0m, Charges: 0m, Expenses: 0m));
        var revenueByBranch = Array.Empty<DashboardSliceDto>(); var expensesByCategory = Array.Empty<DashboardSliceDto>();
        if (sales)
        {
            var daily = await db.SaleEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to)
                .GroupBy(x => new { x.BusinessDate, x.BranchId }).Select(g => new { g.Key.BusinessDate, g.Key.BranchId, Revenue = g.Sum(x => x.Revenue), Cost = g.Sum(x => x.Cogs) }).ToArrayAsync(ct);
            foreach (var d in daily) trend[d.BusinessDate] = trend[d.BusinessDate] with { Revenue = trend[d.BusinessDate].Revenue + d.Revenue, Cost = trend[d.BusinessDate].Cost + d.Cost };
            metrics.Add(new("revenue", "Net revenue", daily.Sum(x => x.Revenue), "money"));
            revenueByBranch = [.. daily.GroupBy(x => x.BranchId).Select(g => new DashboardSliceDto(names[g.Key], g.Sum(x => x.Revenue))).OrderByDescending(x => x.Value)];
        }
        if (profit)
        {
            var charges = await db.CylinderLoanEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.BusinessDate >= from && x.BusinessDate <= to)
                .GroupBy(x => x.BusinessDate).Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.Revenue) }).ToArrayAsync(ct);
            foreach (var c in charges) trend[c.Date] = trend[c.Date] with { Charges = c.Revenue };
            metrics.Add(new("grossProfit", "Gross profit", trend.Values.Sum(x => x.Revenue - x.Cost + x.Charges), "money"));
        }
        if (finance)
        {
            var expenses = await (from e in db.FinanceEntries.AsNoTracking()
                                  join c in db.ExpenseCategories.AsNoTracking() on e.CategoryId equals c.Id
                                  where ids.Contains(e.BranchId) && e.BusinessDate >= @from && e.BusinessDate <= to && (e.Kind == "Expense" || e.Kind == "ExpenseReversal") && !c.Capital
                                  group e by new { e.BusinessDate, c.Name } into g
                                  select new { g.Key.BusinessDate, g.Key.Name, Amount = -g.Sum(x => x.Cash) }).ToArrayAsync(ct);
            foreach (var e in expenses) trend[e.BusinessDate] = trend[e.BusinessDate] with { Expenses = trend[e.BusinessDate].Expenses + e.Amount };
            expensesByCategory = [.. expenses.GroupBy(x => x.Name).Select(g => new DashboardSliceDto(g.Key, g.Sum(x => x.Amount))).OrderByDescending(x => x.Value)];
            metrics.Add(new("expenses", "Operating expenses", expenses.Sum(x => x.Amount), "money"));
            metrics.Add(new("cash", "Cash and bank balance", await db.FinanceEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId) && x.AccountId != null).SumAsync(x => x.Cash, ct), "money"));
            var receivable = await db.SaleEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId)).SumAsync(x => x.Receivable, ct)
                + await db.CylinderLoanEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId)).SumAsync(x => x.Receivable, ct);
            metrics.Add(new("receivables", "Customer receivables", receivable, "money"));
            if (purchases) metrics.Add(new("payables", "Supplier payables", await db.PurchaseEntries.AsNoTracking().Where(x => ids.Contains(x.BranchId)).SumAsync(x => x.Payable, ct), "money"));
        }
        if (inventory)
        {
            // Sellable stock only: available LPG and spare parts, full cylinders.
            var levels = await (from m in db.StockMovements.AsNoTracking()
                                join i in db.StockItems.AsNoTracking() on m.ItemId equals i.Id
                                where ids.Contains(m.BranchId) && i.Active && i.ReorderLevel > 0 && (m.Bucket == "Available" || m.Bucket == "Full")
                                group m by new { i.Id, i.ReorderLevel } into g
                                select new { g.Key.ReorderLevel, Quantity = g.Sum(x => x.Quantity) }).ToArrayAsync(ct);
            var tracked = await db.StockItems.AsNoTracking().CountAsync(i => i.Active && i.ReorderLevel > 0, ct);
            // Items with no sellable movements at all are also at or below their level.
            metrics.Add(new("lowStock", "Items at or below reorder level", levels.Count(x => x.Quantity <= x.ReorderLevel) + (tracked - levels.Length), "count"));
        }
        var points = trend.OrderBy(x => x.Key).Select(x => new DashboardPointDto(Date(x.Key), x.Value.Revenue,
            profit ? x.Value.Revenue - x.Value.Cost + x.Value.Charges : null, finance ? x.Value.Expenses : null)).ToArray();
        return new(from, to, label, [.. metrics], sales || profit || finance ? points : [], expensesByCategory, revenueByBranch, profit, finance);
    }
}
