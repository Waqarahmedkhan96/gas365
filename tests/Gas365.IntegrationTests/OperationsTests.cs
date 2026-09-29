using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    private static async Task<HttpResponseMessage> Upload((HttpClient Client, MeDto Me) o, string type, string fileName, byte[] content)
    {
        var csrf = await o.Client.GetFromJsonAsync<Dictionary<string, string>>("/api/auth/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workspace/imports?type={type}&fileName={Uri.EscapeDataString(fileName)}") { Content = new ByteArrayContent(content) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Add("X-Workspace", o.Me.TenantId.ToString()); request.Headers.Add("X-CSRF-TOKEN", csrf!["token"]);
        return await o.Client.SendAsync(request);
    }
    private static Task<HttpResponseMessage> Imports((HttpClient Client, MeDto Me) o, string path, object? body = null, HttpMethod? method = null) =>
        Send(o.Client, "/api/workspace/imports" + path, method ?? (body is null ? HttpMethod.Get : HttpMethod.Post), body, o.Me.TenantId);
    private static byte[] Csv(params string[] lines) => Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
    private static byte[] Workbook(params string[][] rows)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook();
            var part = workbook.AddNewPart<WorksheetPart>();
            part.Worksheet = new Worksheet(new SheetData(rows.Select(r => new Row(r.Select(v => new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(v)) })))));
            workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = 1, Name = "Data" }));
        }
        return stream.ToArray();
    }
    private async Task Sweep() { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<INotificationSweeper>().SweepAsync(default); }
    private static async Task<NotificationDto[]> Alerts((HttpClient Client, MeDto Me) o) =>
        (await Read<PageDto<NotificationDto>>(await Send(o.Client, "/api/workspace/notifications", HttpMethod.Get, tenant: o.Me.TenantId))).Items;

    [Fact]
    public async Task OpeningBalancesAgeFromOriginalDateSettleByAllocationAndReverseInOrder()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o);
        var customer = await Read<CustomerDto>(await Sales(o, "customers", new CustomerRequest("Hotel Mehran", "", "")));
        var supplier = await Read<SupplierDto>(await Send(o.Client, "/api/workspace/purchasing/suppliers", HttpMethod.Post, new SupplierRequest("Old supplier", "", ""), o.Me.TenantId));
        var request = new OpeningBalanceRequest(branch, customer.Id, null, "Ledger p.14", Today().AddDays(-45), 1000, "Cutover from workbook", Key());
        var opening = await Read<OpeningBalanceDto>(await Finance(o, "openings", request));
        Assert.Equal(opening.Id, (await Read<OpeningBalanceDto>(await Finance(o, "openings", request))).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "openings", request with { Amount = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Finance(o, "openings", request with { IdempotencyKey = Key(), OriginalDate = Today().AddDays(1) })).StatusCode);
        var credit = await Read<CreditDto>(await Finance(o, $"credit?branchId={branch}&partyId={customer.Id}&supplier=false&asOf={Today():yyyy-MM-dd}"));
        var line = credit.Invoices.Single(); Assert.Equal("Opening", line.Kind); Assert.Equal("31–60", line.Bucket); Assert.Equal(1000, credit.Balance);

        var bank = await Account(o, branch, "Bank", 0);
        var advance = await Money(o, branch, bank.Id, "CustomerReceipt", 400, customer: customer.Id);
        var allocation = await Read<FinanceEntryDto>(await Finance(o, "entries", new FinanceRequest(branch, "Allocate", 400, "Settle old balance", Key(), ParentId: advance.Id, OpeningBalanceId: opening.Id)));
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", new FinanceRequest(branch, "Allocate", 1, "Too much", Key(), ParentId: advance.Id, OpeningBalanceId: opening.Id))).StatusCode);
        var aging = await Report(o, "customer-aging"); Assert.Equal(600, Total(aging, "due"));
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, $"openings/{opening.Id}/reverse", new FinanceReverseRequest("Wrong customer", Key()))).StatusCode);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{allocation.Id}/reverse", new FinanceReverseRequest("Allocation error", Key())));
        var reversed = await Read<OpeningBalanceDto>(await Finance(o, $"openings/{opening.Id}/reverse", new FinanceReverseRequest("Wrong customer", Key())));
        Assert.Equal("Reversed", reversed.Status); Assert.Equal(0, reversed.Outstanding);

        await Read<OpeningBalanceDto>(await Finance(o, "openings", new OpeningBalanceRequest(branch, null, supplier.Id, "", Today().AddDays(-10), 250, "Owed at cutover", Key())));
        var profile = await Read<SupplierProfileDto>(await Send(o.Client, $"/api/workspace/purchasing/suppliers/{supplier.Id}?branchId={branch}", HttpMethod.Get, tenant: o.Me.TenantId));
        Assert.Equal(250, profile.Balance); Assert.Equal(250, Total(await Report(o, "supplier-payables"), "due"));

        var other = await Owner(); using var oc = other.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(other, $"openings/{opening.Id}/reverse", new FinanceReverseRequest("Foreign", Key()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(other, "openings", request with { IdempotencyKey = Key() })).StatusCode);
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"OpeningBalanceEntries\" SET \"Amount\" = 1 WHERE \"OpeningBalanceId\" = {opening.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"OpeningBalances\" WHERE \"Id\" = {opening.Id}"));
    }

    [Fact]
    public async Task ImportsMapValidateReportErrorsCommitIdempotentlyAndResume()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o);
        Assert.Equal(6, (await Read<ImportTypeDto[]>(await Imports(o, "/types"))).Length);

        var bad = await Read<ImportBatchDto>(await Upload(o, "customers", "customers.csv", Csv("Client,Phone,Address", "Ali Gas,0300,Karachi", ",0301,No name", "Ali Gas,0300,Duplicate")));
        Assert.Equal(ImportStatus.Invalid, bad.Status); Assert.Null(bad.Mapping["name"]); Assert.Equal(3, bad.ErrorCount);
        var mapped = await Read<ImportBatchDto>(await Imports(o, $"/{bad.Id}/mapping", new ImportMappingRequest(new() { ["name"] = "Client", ["phone"] = "Phone", ["address"] = "Address" }, bad.Version), HttpMethod.Put));
        Assert.Equal(2, mapped.ErrorCount); Assert.Contains(mapped.Preview, r => r.Errors.Any(e => e.Contains("Duplicated earlier")));
        var errors = await Imports(o, $"/{bad.Id}/errors");
        Assert.Equal("text/csv", errors.Content.Headers.ContentType!.MediaType);
        var errorText = await errors.Content.ReadAsStringAsync(); Assert.Contains("3,Name,Required.", errorText); Assert.Contains("4,Name,Duplicated earlier in the file.", errorText);
        Assert.Equal(HttpStatusCode.Conflict, (await Imports(o, $"/{bad.Id}/commit", new ImportCommitRequest(mapped.Version))).StatusCode);

        var good = await Read<ImportBatchDto>(await Upload(o, "customers", "customers.csv", Csv("Name,Phone,Address", "Ali Gas,0300,Karachi", "\"Bilal, Sons\",0301,\"Line 1\nLine 2\"")));
        Assert.Equal(ImportStatus.Validated, good.Status);
        var committed = await Read<ImportBatchDto>(await Imports(o, $"/{good.Id}/commit", new ImportCommitRequest(good.Version)));
        Assert.Equal(ImportStatus.Committed, committed.Status); Assert.Equal(2, committed.CommittedCount);
        Assert.Equal(ImportStatus.Committed, (await Read<ImportBatchDto>(await Imports(o, $"/{good.Id}/commit", new ImportCommitRequest(committed.Version)))).Status);
        await using var db = database.CreateContext();
        Assert.Equal(2, await db.Customers.IgnoreQueryFilters().CountAsync(x => x.TenantId == o.Me.TenantId));
        Assert.Contains("Bilal, Sons", await db.Customers.IgnoreQueryFilters().Where(x => x.TenantId == o.Me.TenantId).Select(x => x.Name).ToArrayAsync());
        Assert.Equal(ImportStatus.Invalid, (await Read<ImportBatchDto>(await Upload(o, "customers", "again.csv", Csv("Name,Phone", "Ali Gas,0300")))).Status);

        var pieces = await Read<UnitDto>(await Stock(o, "units", new UnitRequest("PCS", 0)));
        var kg = await Read<UnitDto>(await Stock(o, "units", new UnitRequest("KG", 3)));
        await Read<CategoryDto>(await Stock(o, "categories", new CategoryRequest("Regulators")));
        var warehouse = await Read<WarehouseDto>(await Stock(o, "warehouses", new WarehouseRequest(branch, "Main store", "")));
        var items = await Read<ImportBatchDto>(await Upload(o, "items", "items.xlsx", Workbook(
            ["SKU", "Name", "Type", "Unit", "Category", "Classification", "Capacity", "Serial tracked", "Reorder level"],
            ["GAS-11", "Domestic LPG", "LPG", "kg", "", "domestic", "11.8", "No", "20"],
            ["CYL-11", "11.8 kg cylinder", "Cylinder", "PCS", "", "Domestic", "11.8", "Yes", "0"],
            ["REG-1", "Regulator", "Spare part", "PCS", "Regulators", "", "", "", "5"])));
        Assert.Equal(ImportStatus.Validated, items.Status);
        Assert.Equal(ImportStatus.Committed, (await Read<ImportBatchDto>(await Imports(o, $"/{items.Id}/commit", new ImportCommitRequest(items.Version)))).Status);

        var stock = await Read<ImportBatchDto>(await Upload(o, "opening-stock", "stock.csv", Csv("SKU,Warehouse,Quantity,Unit cost,Serials", "CYL-11,Main store,1,1000,S-1", "GAS-11,Main store,10.5,50,")));
        Assert.Equal(ImportStatus.Validated, stock.Status);
        var partial = await Read<ImportBatchDto>(await Imports(o, $"/{stock.Id}/commit", new ImportCommitRequest(stock.Version)));
        Assert.Equal(ImportStatus.PartiallyCommitted, partial.Status); Assert.Equal(1, partial.CommittedCount);
        Assert.Contains(partial.Preview, r => r.Status == ImportStatus.Failed && r.Errors.Any(e => e.Contains("not registered")));
        var cylinder = await db.StockItems.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.Sku == "CYL-11");
        await Read<AssetDto>(await Stock(o, "assets", new AssetRequest(cylinder.Id, "S-1", "Business", "", null)));
        var resumed = await Read<ImportBatchDto>(await Imports(o, $"/{stock.Id}/commit", new ImportCommitRequest(partial.Version)));
        Assert.Equal(ImportStatus.Committed, resumed.Status);
        var gas = await db.StockItems.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.Sku == "GAS-11");
        Assert.Equal(10.5m, await db.StockMovements.IgnoreQueryFilters().Where(x => x.TenantId == o.Me.TenantId && x.ItemId == gas.Id).SumAsync(x => x.Quantity));
        Assert.Equal(2, await db.StockDocuments.IgnoreQueryFilters().CountAsync(x => x.TenantId == o.Me.TenantId && x.IdempotencyKey.StartsWith("import:")));
        Assert.Equal(ImportStatus.Invalid, (await Read<ImportBatchDto>(await Upload(o, "opening-stock", "again.csv", Csv("SKU,Warehouse,Quantity,Unit cost", "GAS-11,Main store,1,50")))).Status);

        var balances = await Read<ImportBatchDto>(await Upload(o, "customer-balances", "udhaar.csv",
            Csv("Customer,Branch,Amount,Original invoice date,Reference", $"Ali Gas,{(await Read<BranchDto[]>(await Send(o.Client, "/api/workspace/branches", HttpMethod.Get, tenant: o.Me.TenantId)))[0].Name},\"1,250.50\",{Today().AddDays(-40):dd/MM/yyyy},Old ledger")));
        Assert.Equal(ImportStatus.Validated, balances.Status);
        Assert.Equal(ImportStatus.Committed, (await Read<ImportBatchDto>(await Imports(o, $"/{balances.Id}/commit", new ImportCommitRequest(balances.Version)))).Status);
        Assert.Equal(1250.50m, Total(await Report(o, "customer-aging"), "b2"));

        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(o, "customers", "old.xls", [1, 2, 3])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(o, "customers", "fake.xlsx", Csv("Name", "A"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(o, "customers", "binary.csv", [0x4E, 0x00, 0x41])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(o, "customers", "many.csv", Csv([.. new[] { "Name" }, .. Enumerable.Range(0, 5001).Select(i => "C" + i)]))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(o, "customers", "huge.csv", new byte[ImportStatus.MaxBytes + 10])).StatusCode);

        var other = await Owner(); using var oc = other.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await Imports(other, $"/{good.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Imports(other, $"/{good.Id}/errors")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Imports(other, $"/{stock.Id}/commit", new ImportCommitRequest(resumed.Version))).StatusCode);
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        string[] withoutImports = [.. role.Permissions.Where(p => p != Permissions.ImportsManage)];
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {withoutImports} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Imports(o, "/types")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(o, "customers", "c.csv", Csv("Name", "X"))).StatusCode);
    }

    [Fact]
    public async Task NotificationsAppearResolveRespectPermissionsAndTrackReads()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o);
        var (item, warehouse) = await Setup(o); // Reorder level 2.
        await Read<DocumentDto>(await Stock(o, "documents", new PostStockRequest(Key(), "Opening", item.Id, warehouse.Id, "Available", 1, 10, null, null, "Opening", 0, 0, [])));
        var customer = await Read<CustomerDto>(await Sales(o, "customers", new CustomerRequest("Late payer", "", "")));
        await Read<OpeningBalanceDto>(await Finance(o, "openings", new OpeningBalanceRequest(branch, customer.Id, null, "", Today().AddDays(-45), 900, "Cutover", Key())));
        var cash = await Account(o, branch, opening: 0); var register = await Open(o, cash.Id);
        (await Send(o.Client, "/api/workspace/tenant", HttpMethod.Put, new TenantRequest("Inventory tenant", "Asia/Karachi", 30, new TimeOnly(0, 0)), o.Me.TenantId)).EnsureSuccessStatusCode();

        await Sweep(); await Sweep();
        var alerts = await Alerts(o);
        Assert.Equal(3, alerts.Length);
        Assert.Contains(alerts, a => a.Kind == NotificationKinds.LowStock && a.Body.Contains("reorder level 2"));
        Assert.Contains(alerts, a => a.Kind == NotificationKinds.OverdueCredit && a.Body.Contains("900.00") && a.Body.Contains("30 days"));
        Assert.Contains(alerts, a => a.Kind == NotificationKinds.RegisterOpen);
        Assert.Equal(3, (await Read<NotificationSummaryDto>(await Send(o.Client, "/api/workspace/notifications/summary", HttpMethod.Get, tenant: o.Me.TenantId))).Unread);
        (await Send(o.Client, $"/api/workspace/notifications/{alerts[0].Id}/read", HttpMethod.Post, tenant: o.Me.TenantId)).EnsureSuccessStatusCode();
        Assert.Equal(2, (await Read<NotificationSummaryDto>(await Send(o.Client, "/api/workspace/notifications/summary", HttpMethod.Get, tenant: o.Me.TenantId))).Unread);
        (await Send(o.Client, "/api/workspace/notifications/read-all", HttpMethod.Post, tenant: o.Me.TenantId)).EnsureSuccessStatusCode();
        Assert.All(await Alerts(o), a => Assert.True(a.Read));

        await Read<DocumentDto>(await Stock(o, "documents", new PostStockRequest(Key(), "AdjustmentIn", item.Id, warehouse.Id, "Available", 5, 10, null, null, "Delivery", (await Read<StockBalanceDto[]>(await Stock(o, $"items/{item.Id}/balances"))).Max(x => x.Sequence), 0, [])));
        (await Send(o.Client, "/api/workspace/tenant", HttpMethod.Put, new TenantRequest("Inventory tenant", "Asia/Karachi", 60, new TimeOnly(0, 0)), o.Me.TenantId)).EnsureSuccessStatusCode();
        await Read<RegisterDto>(await Finance(o, "register", new RegisterRequestDto(cash.Id, Today(), "Close", 0, "Counted", Key(), register.LastEventId)));
        await Sweep();
        Assert.Empty(await Alerts(o));
        await using var db = database.CreateContext();
        Assert.Equal(3, await db.Notifications.IgnoreQueryFilters().CountAsync(x => x.TenantId == o.Me.TenantId && x.ResolvedAt != null));

        (await Send(o.Client, "/api/workspace/tenant", HttpMethod.Put, new TenantRequest("Inventory tenant", "Asia/Karachi", 30, null), o.Me.TenantId)).EnsureSuccessStatusCode();
        await Sweep();
        var overdue = (await Alerts(o)).Single(); Assert.Equal(NotificationKinds.OverdueCredit, overdue.Kind);
        var other = await Owner(); using var oc = other.Client;
        Assert.Empty(await Alerts(other));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(other.Client, $"/api/workspace/notifications/{overdue.Id}/read", HttpMethod.Post, tenant: other.Me.TenantId)).StatusCode);
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        string[] withoutFinance = [.. role.Permissions.Where(p => p != Permissions.FinanceView)];
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {withoutFinance} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Empty(await Alerts(o));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(o.Client, "/api/workspace/tenant", HttpMethod.Put, new TenantRequest("Inventory tenant", "Asia/Karachi", 0, null), o.Me.TenantId)).StatusCode);
    }

    [Fact]
    public async Task AuditTimelineAndCorrectionExplorerFilterAndIsolate()
    {
        var (o, branch, _) = await ReportingTenant(); using var client = o.Client;
        var range = $"from={Today():yyyy-MM-dd}&to={Today():yyyy-MM-dd}";
        var timeline = await Read<PageDto<AuditEntryDto>>(await Send(o.Client, $"/api/workspace/audit/timeline?{range}", HttpMethod.Get, tenant: o.Me.TenantId));
        Assert.True(timeline.Total > 20); Assert.Equal(20, timeline.Items.Length); Assert.All(timeline.Items, x => Assert.NotEqual("Former member", x.Actor));
        var finance = await Read<PageDto<AuditEntryDto>>(await Send(o.Client, $"/api/workspace/audit/timeline?{range}&area=finance", HttpMethod.Get, tenant: o.Me.TenantId));
        Assert.NotEmpty(finance.Items); Assert.All(finance.Items, x => Assert.Equal("finance", x.Area));
        var subject = finance.Items[0].SubjectId!.Value;
        Assert.All((await Read<PageDto<AuditEntryDto>>(await Send(o.Client, $"/api/workspace/audit/timeline?{range}&subjectId={subject}", HttpMethod.Get, tenant: o.Me.TenantId))).Items, x => Assert.Equal(subject, x.SubjectId));

        var corrections = await Read<PageDto<CorrectionDto>>(await Send(o.Client, $"/api/workspace/audit/corrections?{range}", HttpMethod.Get, tenant: o.Me.TenantId));
        Assert.Contains(corrections.Items, x => x.Module == "sales" && x.Kind == "Reversal" && x.Reason == "Entered twice");
        Assert.Contains(corrections.Items, x => x.Module == "finance" && x.Reason == "Duplicate" && x.Amount == 5);
        Assert.Contains(corrections.Items, x => x.Module == "stock");
        Assert.All((await Read<PageDto<CorrectionDto>>(await Send(o.Client, $"/api/workspace/audit/corrections?{range}&module=finance", HttpMethod.Get, tenant: o.Me.TenantId))).Items, x => Assert.Equal("finance", x.Module));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(o.Client, $"/api/workspace/audit/corrections?{range}&module=secret", HttpMethod.Get, tenant: o.Me.TenantId)).StatusCode);

        var other = await Owner(); using var oc = other.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await Send(other.Client, $"/api/workspace/audit/corrections?{range}&branchId={branch}", HttpMethod.Get, tenant: other.Me.TenantId)).StatusCode);
        Assert.Empty((await Read<PageDto<CorrectionDto>>(await Send(other.Client, $"/api/workspace/audit/corrections?{range}", HttpMethod.Get, tenant: other.Me.TenantId))).Items);
        await using var db = database.CreateContext();
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        string[] withoutAudit = [.. role.Permissions.Where(p => p != Permissions.AuditView)];
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {withoutAudit} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(o.Client, $"/api/workspace/audit/timeline?{range}", HttpMethod.Get, tenant: o.Me.TenantId)).StatusCode);
    }

    [Fact]
    public async Task FailingExportJobsRetryThenFailAndJobHealthIsRecorded()
    {
        var o = await Owner(); using var client = o.Client;
        await using var db = database.CreateContext();
        var member = await db.Memberships.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId);
        var id = Guid.NewGuid();
        // A corrupt filter makes every attempt throw an unexpected error, exercising retry and final failure.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ExportJobs" ("TenantId","Id","MembershipId","UserId","ReportKey","Format","Filter","Status","Attempts","Error","FileName","ContentType","RowCount","RequestedAt","IdempotencyKey","RequestHash")
            VALUES ({o.Me.TenantId},{id},{member.Id},{member.UserId},'daily-sales','csv','not json','Queued',0,'','broken.csv','',0,now(),{Key()},'')
            """);
        ExportJobDto? job = null;
        for (var i = 0; i < 150 && job?.Status != ExportStatus.Failed; i++)
        {
            await Task.Delay(200);
            job = (await Read<PageDto<ExportJobDto>>(await Reports(o, "/exports"))).Items.Single(x => x.Id == id);
        }
        Assert.Equal(ExportStatus.Failed, job!.Status); Assert.Equal(ExportStatus.MaxAttempts, job.Attempts); Assert.Equal("Export failed after repeated attempts.", job.Error);
        var health = await db.JobStates.SingleOrDefaultAsync(x => x.Name == "exports");
        Assert.NotNull(health); Assert.NotNull(health.LastSucceededAt);
    }
}
