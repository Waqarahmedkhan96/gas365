using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    private static Task<HttpResponseMessage> Reports((HttpClient Client, MeDto Me) o, string path, object? body = null) =>
        Send(o.Client, "/api/workspace/reports" + path, body is null ? HttpMethod.Get : HttpMethod.Post, body, o.Me.TenantId);
    private static async Task<ReportDto> Report((HttpClient Client, MeDto Me) o, string key, string query = "") =>
        await Read<ReportDto>(await Reports(o, $"/{key}?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}{query}"));
    private static decimal Total(ReportDto r, string key) => ((JsonElement)r.Totals[Array.FindIndex(r.Columns, c => c.Key == key)]!).GetDecimal();
    private static decimal Cell(object? value) => value is null ? 0 : ((JsonElement)value).ValueKind == JsonValueKind.Null ? 0 : ((JsonElement)value).GetDecimal();
    private static object?[] Line(ReportDto r, string label) => r.Rows.Single(x => ((JsonElement)x[0]!).GetString() == label);

    // One tenant with every ledger populated: multi-category sales, a reversed sale, a purchase with payment,
    // operating and capital expenses with a reversal, cylinder loans with a usage fee and drawer cash.
    private async Task<((HttpClient Client, MeDto Me) Owner, Guid Branch, Guid Customer)> ReportingTenant()
    {
        var o = await Owner(); var branch = await BranchId(o);
        var cash = await Account(o, branch, opening: 0); await Open(o, cash.Id);
        var (gas, gasStore) = await Setup(o); var partStore = gasStore;
        var pieces = await Read<UnitDto>(await Stock(o, "units", new UnitRequest("PCS" + Key()[..4], 0)));
        var regulators = await Read<CategoryDto>(await Stock(o, "categories", new CategoryRequest("Regulators " + Key()[..6])));
        var part = await Read<ItemDto>(await Stock(o, "items", new ItemRequest("REG" + Key()[..6], "Regulator", "SparePart", pieces.Id, regulators.Id, "", "", null, null, false, 2)));
        await Read<DocumentDto>(await Stock(o, "documents", new PostStockRequest(Key(), "Opening", gas.Id, gasStore.Id, "Available", 100, 50, null, null, "Opening", 0, 0, [])));
        await Read<DocumentDto>(await Stock(o, "documents", new PostStockRequest(Key(), "Opening", part.Id, partStore.Id, "Available", 10, 200, null, null, "Opening", 0, 0, [])));
        var customer = await Read<CustomerDto>(await Sales(o, "customers", new CustomerRequest("Report customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(o, "price-lists", new PriceListRequest("Report prices")));
        var gasPrice = await Read<SalePriceDto>(await Sales(o, $"price-lists/{list.Id}/prices", new SalePriceRequest(gas.Id, "Refill", 120)));
        var partPrice = await Read<SalePriceDto>(await Sales(o, $"price-lists/{list.Id}/prices", new SalePriceRequest(part.Id, "SparePart", 500)));
        var mixed = await Read<SaleDraftDto>(await Sales(o, "drafts", new SaleDraftRequest(branch, customer.Id, list.Id, "Mixed", "", [new(gasPrice.Id, gasPrice.Version, gasStore.Id, 3, 0), new(partPrice.Id, partPrice.Version, partStore.Id, 2, 0)])));
        await Read<SaleDto>(await Sales(o, $"drafts/{mixed.Id}/post", new SalePostRequest(Key(), mixed.Version, 400, [[], []])));
        var reversed = await Read<SaleDraftDto>(await Sales(o, "drafts", new SaleDraftRequest(branch, customer.Id, list.Id, "Reversed", "", [new(gasPrice.Id, gasPrice.Version, gasStore.Id, 1, 0), new(partPrice.Id, partPrice.Version, partStore.Id, 1, 0)])));
        var sale = await Read<SaleDto>(await Sales(o, $"drafts/{reversed.Id}/post", new SalePostRequest(Key(), reversed.Version, 0, [[], []])));
        await Read<SaleDto>(await Sales(o, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Key(), "Entered twice")));
        var supplier = await Read<SupplierDto>(await Send(o.Client, "/api/workspace/purchasing/suppliers", HttpMethod.Post, new SupplierRequest("Report supplier", "", ""), o.Me.TenantId));
        var purchase = await Read<PurchaseDto>(await Send(o.Client, "/api/workspace/purchasing/purchases", HttpMethod.Post, new PurchaseDraftRequest(branch, supplier.Id, "P-1", "", "Value", 0, [new(gas.Id, gasStore.Id, "Available", 5, 60, 5, [])]), o.Me.TenantId));
        await Read<PurchaseDto>(await Send(o.Client, $"/api/workspace/purchasing/purchases/{purchase.Id}/post", HttpMethod.Post, new PurchasePostRequest(Key(), purchase.Version, 100), o.Me.TenantId));
        var operating = await Read<ExpenseCategoryDto>(await Finance(o, "categories", new ExpenseCategoryRequest("Electricity " + Key()[..6], false)));
        var capital = await Read<ExpenseCategoryDto>(await Finance(o, "categories", new ExpenseCategoryRequest("Scale " + Key()[..6], true)));
        await Money(o, branch, cash.Id, "OwnerFunds", 1000);
        await Money(o, branch, cash.Id, "Expense", 70, category: operating.Id);
        await Money(o, branch, cash.Id, "Expense", 30, category: capital.Id);
        var wrong = await Money(o, branch, cash.Id, "Expense", 5, category: operating.Id);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{wrong.Id}/reverse", new FinanceReverseRequest("Duplicate", Key())));
        var (draft, _, _, _) = await CylinderDraft(o, count: 2);
        var loans = await Read<SaleDto>(await Sales(o, $"drafts/{draft.Id}/post", new SalePostRequest(Key(), draft.Version, 0, [["FULL-1", "FULL-2"]], [new([], 1000)])));
        var loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loans.LoanIds![0]}"));
        await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}/return", new LoanReturnRequest(Key(), loan.Entries[^1].Id, "Empty", 1500, null, "Usage fee")));
        return (o, branch, customer.Id);
    }

    [Fact]
    public async Task EveryReportReconcilesToItsLedger()
    {
        var (o, branch, customer) = await ReportingTenant(); using var client = o.Client; var tenant = o.Me.TenantId;
        await using var db = database.CreateContext();
        var sales = await db.SaleEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenant).ToArrayAsync();
        var purchases = await db.PurchaseEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenant).ToArrayAsync();
        var cylinders = await db.CylinderLoanEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenant).ToArrayAsync();
        var finance = await db.FinanceEntries.IgnoreQueryFilters().Where(x => x.TenantId == tenant).ToArrayAsync();
        var stockValue = await db.StockMovements.IgnoreQueryFilters().Where(x => x.TenantId == tenant).SumAsync(x => x.Value);
        var customerHeld = await db.StockMovements.IgnoreQueryFilters().Where(x => x.TenantId == tenant && x.Bucket == "CustomerHeld").SumAsync(x => x.Quantity);

        var definitions = await Read<ReportDefinitionDto[]>(await Reports(o, ""));
        Assert.Equal(15, definitions.Length); Assert.All(definitions, d => Assert.NotEmpty(d.Reconciliation));

        var daily = await Report(o, "daily-sales");
        Assert.Equal(sales.Sum(x => x.Revenue), Total(daily, "net")); Assert.Equal(sales.Sum(x => x.Receivable), Total(daily, "receivable"));
        Assert.Equal(sales.Where(x => x.Kind == "Payment").Sum(x => x.Cash), Total(daily, "cash")); Assert.Equal(3, Total(daily, "invoices"));

        var lpg = await Report(o, "lpg-profit"); var spare = await Report(o, "spare-parts-profit");
        Assert.Equal(sales.Sum(x => x.Revenue), Total(lpg, "revenue") + Total(spare, "revenue"));
        Assert.Equal(sales.Sum(x => x.Cogs), Total(lpg, "cost") + Total(spare, "cost"));
        Assert.Equal(1000, Total(spare, "revenue")); Assert.Equal(400, Total(spare, "cost")); // Two parts at 500, costed at 200; the reversed sale nets to zero.

        var charges = cylinders.Sum(x => x.Revenue); Assert.Equal(1500, charges);
        var profit = await Report(o, "profit");
        var gross = sales.Sum(x => x.Revenue) + charges - sales.Sum(x => x.Cogs);
        Assert.Equal(sales.Sum(x => x.Revenue) + charges, Cell(Line(profit, "Total revenue")[1]));
        Assert.Equal(gross, Cell(Line(profit, "Gross profit")[1])); Assert.Equal(70, Cell(Line(profit, "Operating expenses")[1]));
        Assert.Equal(gross - 70, Cell(Line(profit, "Net profit")[1])); Assert.Equal(30, Cell(Line(profit, "Memo: capital expenses (excluded)")[1]));

        var expenses = await Report(o, "expenses");
        Assert.Equal(-finance.Where(x => x.Kind is "Expense" or "ExpenseReversal").Sum(x => x.Cash), Total(expenses, "amount")); Assert.Equal(100, Total(expenses, "amount"));
        Assert.Equal(70, Total(await Report(o, "expenses", "&type=Operating"), "amount"));

        var bought = await Report(o, "purchases");
        Assert.Equal(purchases.Where(x => x.Kind is "Invoice" or "Reversal").Sum(x => x.Payable), Total(bought, "net"));
        Assert.Equal(purchases.Sum(x => x.Payable), Total(bought, "payable")); Assert.Equal(100, Total(bought, "paid"));

        var onHand = await Report(o, "stock-on-hand"); var valuation = await Report(o, "inventory-valuation"); var movement = await Report(o, "stock-movement");
        Assert.Equal(stockValue, Total(onHand, "value")); Assert.Equal(stockValue, Total(valuation, "value")); Assert.Equal(stockValue, Total(movement, "closingValue"));
        Assert.All(movement.Rows, r => Assert.Equal(Cell(r[5]) + Cell(r[6]) - Cell(r[7]), Cell(r[8])));
        Assert.Equal(Total(movement, "openingValue") + Total(movement, "valueChange"), Total(movement, "closingValue"));
        Assert.Equal(1600, Total(await Report(o, "stock-on-hand", "&type=SparePart"), "value")); // Ten parts at 200 less two sold; the reversed sale returned its part.

        var aging = await Report(o, "customer-aging");
        Assert.Equal(sales.Sum(x => x.Receivable) + cylinders.Sum(x => x.Receivable), Total(aging, "due"));
        Assert.Equal(finance.Where(x => x.CustomerId != null).Sum(x => x.Amount), Total(aging, "unapplied"));
        var credit = await Read<CreditDto>(await Finance(o, $"credit?branchId={branch}&partyId={customer}&supplier=false&asOf={Today():yyyy-MM-dd}"));
        Assert.Equal(credit.Balance, Cell(aging.Rows.Single(r => ((JsonElement)r[0]!).GetString() == "Report customer")[^1]));
        Assert.Equal(purchases.Sum(x => x.Payable), Total(await Report(o, "supplier-payables"), "due"));

        var cash = await Report(o, "cash-reconciliation");
        var drawer = (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single();
        var register = await Read<RegisterDto>(await Finance(o, $"register?accountId={drawer.Id}&date={Today():yyyy-MM-dd}"));
        var row = cash.Rows.Single(); var expected = Cell(row[Array.FindIndex(cash.Columns, c => c.Key == "expected")]);
        Assert.Equal(register.Expected, expected); Assert.Equal(drawer.Balance, expected);
        Assert.Equal(expected, Cell(row[3]) + ReportCalculation.CashCategories.Select((_, i) => Cell(row[4 + i])).Sum());
        Assert.Equal(400, Cell(row[4])); Assert.Equal(2000, Cell(row[6])); Assert.Equal(-100, Cell(row[7])); Assert.Equal(-100, Cell(row[9])); Assert.Equal(1000, Cell(row[10]));

        var custody = await Report(o, "cylinder-custody");
        Assert.Equal(customerHeld, Total(custody, "onLoan")); Assert.Equal(1, Total(custody, "onLoan"));
        Assert.Equal(cylinders.Sum(x => x.Liability), Total(custody, "deposits")); Assert.Equal(cylinders.Sum(x => x.Receivable), Total(custody, "due"));

        var branches = await Report(o, "branch-comparison");
        Assert.Equal(sales.Sum(x => x.Revenue), Total(branches, "revenue")); Assert.Equal(gross, Total(branches, "gross")); Assert.Equal(gross - 70, Total(branches, "net"));
        Assert.Equal(Total(bought, "net"), Total(branches, "purchases"));

        var activity = await Report(o, "user-activity");
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi"); var start = new DateTimeOffset(Today().ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(DateTime.UtcNow)).ToUniversalTime();
        Assert.Equal(await db.AuditEvents.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenant && x.OccurredAt >= start && x.OccurredAt < start.AddDays(1)), Total(activity, "count"));
        Assert.Contains(activity.Rows, r => ((JsonElement)r[1]!).GetString() == "finance.reversed");
        var financeActivity = await Report(o, "user-activity", "&type=finance");
        Assert.NotEmpty(financeActivity.Rows);
        Assert.All(financeActivity.Rows, r => Assert.Contains(((JsonElement)r[1]!).GetString()!.Split('.')[0], new[] { "finance", "register" }));

        var dashboard = await Read<DashboardDto>(await Reports(o, $"/dashboard?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}"));
        Assert.Equal(sales.Sum(x => x.Revenue), dashboard.Metrics.Single(x => x.Key == "revenue").Value);
        Assert.Equal(gross, dashboard.Metrics.Single(x => x.Key == "grossProfit").Value); Assert.Equal(70, dashboard.Metrics.Single(x => x.Key == "expenses").Value);
        Assert.Equal(sales.Sum(x => x.Receivable) + cylinders.Sum(x => x.Receivable), dashboard.Metrics.Single(x => x.Key == "receivables").Value);
        Assert.Equal(gross, dashboard.Trend.Single().GrossProfit);
    }

    [Fact]
    public async Task ReportsRespectPermissionsFiltersAndTenantIsolation()
    {
        var (o, branch, _) = await ReportingTenant(); using var client = o.Client;
        var other = await Owner(); using var oc = other.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await Reports(other, $"/daily-sales?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}&branchId={branch}")).StatusCode);
        Assert.Empty((await Report(other, "daily-sales")).Rows);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reports(o, $"/daily-sales?from={Today():yyyy-MM-dd}&to={Today().AddDays(-1):yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reports(o, $"/daily-sales?from={Today().AddDays(-400):yyyy-MM-dd}&to={Today():yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reports(o, $"/expenses?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}&type=Unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Reports(o, $"/secret?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}")).StatusCode);

        await using var db = database.CreateContext();
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        string[] limited = [Permissions.ReportsView, Permissions.SalesView, Permissions.InventoryView];
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {limited} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        var visible = (await Read<ReportDefinitionDto[]>(await Reports(o, ""))).Select(x => x.Key).ToArray();
        Assert.Contains("daily-sales", visible); Assert.DoesNotContain("profit", visible); Assert.DoesNotContain("cash-reconciliation", visible);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reports(o, $"/profit?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}")).StatusCode);
        var dashboard = await Read<DashboardDto>(await Reports(o, $"/dashboard?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}"));
        Assert.False(dashboard.ProfitVisible); Assert.DoesNotContain(dashboard.Metrics, m => m.Key is "grossProfit" or "cash" or "receivables");
        Assert.All(dashboard.Trend, p => { Assert.Null(p.GrossProfit); Assert.Null(p.Expenses); });
        Assert.Equal(HttpStatusCode.Forbidden, (await Reports(o, "/exports", new ExportRequest("daily-sales", "csv", null, Today(), Today(), null, Key()))).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {Array.Empty<string>()} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Reports(o, "")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reports(o, $"/dashboard?from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}")).StatusCode);
    }

    private static async Task<ExportJobDto> Finished((HttpClient Client, MeDto Me) o, Guid id)
    {
        for (var i = 0; i < 150; i++)
        {
            var job = (await Read<PageDto<ExportJobDto>>(await Reports(o, "/exports"))).Items.Single(x => x.Id == id);
            if (job.Status is ExportStatus.Completed or ExportStatus.Failed) return job;
            await Task.Delay(200);
        }
        throw new TimeoutException("Export did not finish.");
    }

    [Fact]
    public async Task ExportsRunInTheBackgroundAndDownloadsArePrivateAuditedAndExpire()
    {
        var (o, _, _) = await ReportingTenant(); using var client = o.Client;
        var request = new ExportRequest("daily-sales", "csv", null, Today(), Today(), null, Key());
        var queued = await Read<ExportJobDto>(await Reports(o, "/exports", request));
        Assert.Equal(queued.Id, (await Read<ExportJobDto>(await Reports(o, "/exports", request))).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Reports(o, "/exports", request with { Format = "pdf" })).StatusCode);
        var done = await Finished(o, queued.Id);
        Assert.Equal(ExportStatus.Completed, done.Status); Assert.Equal(1, done.RowCount); Assert.Equal($"gas365-daily-sales-{Today():yyyyMMdd}-{Today():yyyyMMdd}.csv", done.FileName);
        Assert.True(done.ExpiresAt > DateTimeOffset.UtcNow.AddHours(23));
        var download = await Reports(o, $"/exports/{done.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal("text/csv", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        var csv = Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());
        var report = await Report(o, "daily-sales");
        Assert.Contains(Total(report, "net").ToString(CultureInfo.InvariantCulture), csv);

        foreach (var format in new[] { "xlsx", "pdf" })
        {
            var job = await Finished(o, (await Read<ExportJobDto>(await Reports(o, "/exports", new ExportRequest("profit", format, null, Today(), Today(), null, Key())))).Id);
            Assert.Equal(ExportStatus.Completed, job.Status);
            var bytes = await (await Reports(o, $"/exports/{job.Id}/download")).Content.ReadAsByteArrayAsync();
            Assert.Equal(format == "pdf" ? "%PDF"u8.ToArray() : "PK"u8.ToArray(), bytes[..(format == "pdf" ? 4 : 2)]);
        }

        var other = await Owner(); using var oc = other.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await Reports(other, $"/exports/{done.Id}/download")).StatusCode);
        Assert.DoesNotContain((await Read<PageDto<ExportJobDto>>(await Reports(other, "/exports"))).Items, x => x.Id == done.Id);

        await using var db = database.CreateContext();
        var actions = await db.AuditEvents.IgnoreQueryFilters().Where(x => x.TenantId == o.Me.TenantId && x.SubjectId == done.Id).Select(x => x.Action).ToArrayAsync();
        Assert.Contains("report.export_requested", actions); Assert.Contains("report.export_completed", actions); Assert.Contains("report.export_downloaded", actions);

        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        string[] withoutSales = [.. role.Permissions.Where(p => p != Permissions.SalesView)];
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {withoutSales} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Reports(o, $"/exports/{done.Id}/download")).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {role.Permissions} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ExportJobs\" SET \"ExpiresAt\" = now() - interval '1 minute' WHERE \"Id\" = {done.Id}");
        Assert.Equal(HttpStatusCode.Gone, (await Reports(o, $"/exports/{done.Id}/download")).StatusCode);
    }
}
