using System.Net;
using System.Net.Http.Json;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gas365.IntegrationTests;

public sealed class InventoryTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string Password = "Synthetic-Inventory-2026!";
    private readonly TestMessages messages = new();
    private WebApplicationFactory<Program> app = null!;
    public async Task InitializeAsync()
    {
        await using var db = database.CreateContext(); await db.Database.MigrateAsync();
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = database.ConnectionString }));
            builder.ConfigureServices(s => { s.RemoveAll<IAccountMessages>(); s.AddSingleton<IAccountMessages>(messages); });
        });
    }
    public async Task DisposeAsync() => await app.DisposeAsync();
    private HttpClient Client() => app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, HttpMethod method, object? body = null, Guid? tenant = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (tenant.HasValue) request.Headers.Add("X-Workspace", tenant.ToString());
        if (method != HttpMethod.Get)
        {
            var csrf = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", csrf!["token"]); request.Content = JsonContent.Create(body ?? new { });
        }
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, MeDto Me)> Owner()
    {
        var client = Client(); var email = $"stock-{Guid.NewGuid():N}@test.example";
        (await Send(client, "/api/auth/register", HttpMethod.Post, new RegisterRequest(email, Password, "Inventory owner", "Inventory tenant"))).EnsureSuccessStatusCode();
        (await Send(client, "/api/auth/confirm", HttpMethod.Post, new ConfirmRequest(email, messages.Token(email)))).EnsureSuccessStatusCode();
        (await Send(client, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        return (client, (await client.GetFromJsonAsync<MeDto>("/api/auth/me"))!);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>())!; }
    private static Task<HttpResponseMessage> Stock((HttpClient Client, MeDto Me) owner, string path, object? body = null, HttpMethod? method = null) =>
        Send(owner.Client, "/api/workspace/inventory/" + path, method ?? (body is null ? HttpMethod.Get : HttpMethod.Post), body, owner.Me.TenantId);
    private static async Task<(ItemDto Item, WarehouseDto Warehouse)> Setup((HttpClient Client, MeDto Me) owner, string kind = "Lpg", bool serial = false)
    {
        var unit = await Read<UnitDto>(await Stock(owner, "units", new UnitRequest("U" + Guid.NewGuid().ToString("N")[..8], kind == "Cylinder" ? 0 : 3)));
        var branches = await Read<BranchDto[]>(await Send(owner.Client, "/api/workspace/branches", HttpMethod.Get, tenant: owner.Me.TenantId));
        var warehouse = await Read<WarehouseDto>(await Stock(owner, "warehouses", new WarehouseRequest(branches[0].Id, "Store " + Guid.NewGuid(), "Rack A")));
        var item = await Read<ItemDto>(await Stock(owner, "items", new ItemRequest("SKU" + Guid.NewGuid().ToString("N")[..8], "Synthetic stock", kind, unit.Id, null, "Original", "Domestic", 11.8m, 12m, serial, 2)));
        return (item, warehouse);
    }
    private static PostStockRequest Command(ItemDto item, WarehouseDto warehouse, decimal quantity, decimal? cost = 100, string kind = "Opening", long sequence = 0) =>
        new(Guid.NewGuid().ToString("N"), kind, item.Id, warehouse.Id, item.Kind == "Cylinder" ? "Empty" : "Available", quantity, cost, null, null, "Synthetic reconciliation", sequence, 0, []);
    private static async Task<StockBalanceDto[]> Balances((HttpClient Client, MeDto Me) owner, ItemDto item) => await Read<StockBalanceDto[]>(await Stock(owner, $"items/{item.Id}/balances"));

    [Fact]
    public async Task ReceiptsIssuesTransfersAndReversalConserveQuantityAndValue()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, source) = await Setup(owner);
        var opening = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, source, 10)));
        var receipt = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, source, 10, 120, "AdjustmentIn", opening.Movements[0].Sequence)));
        var balance = Assert.Single(await Balances(owner, item)); Assert.Equal(20, balance.Quantity); Assert.Equal(2200, balance.WarehouseValue); Assert.Equal(110, balance.AverageCost);
        var issue = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, source, 5, null, "AdjustmentOut", balance.Sequence)));
        Assert.Equal(-550, Assert.Single(issue.Movements).Value);
        var secondBranch = await Read<BranchDto>(await Send(client, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Second branch"), owner.Me.TenantId));
        var destination = await Read<WarehouseDto>(await Stock(owner, "warehouses", new WarehouseRequest(secondBranch.Id, "Other", "Rack B")));
        var transfer = Command(item, source, 5, null, "Transfer", issue.Movements[0].Sequence) with { DestinationWarehouseId = destination.Id, DestinationBucket = "Available" };
        var posted = await Read<DocumentDto>(await Stock(owner, "documents", transfer)); Assert.Equal(0, posted.Movements.Sum(x => x.Value)); Assert.Equal(0, posted.Movements.Sum(x => x.Quantity));
        var balances = await Balances(owner, item); Assert.Equal(15, balances.Sum(x => x.Quantity)); Assert.Equal(1650, balances.Sum(x => x.WarehouseValue));
        var reversal = await Read<DocumentDto>(await Stock(owner, $"documents/{posted.Id}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Wrong destination")));
        Assert.Equal(source.BranchId, posted.BranchId); Assert.Equal(posted.BranchId, reversal.BranchId); Assert.Equal(owner.Me.UserId, reversal.ActorId);
        Assert.Equal(posted.Id, reversal.ReversesId); Assert.Equal(0, posted.Movements.Sum(x => x.Value) + reversal.Movements.Sum(x => x.Value));
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, $"documents/{opening.Id}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Dependent receipt"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, $"documents/{posted.Id}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Again"))).StatusCode);
        var original = await Read<DocumentDto>(await Stock(owner, $"documents/{issue.Id}")); Assert.Equal(-550, original.Movements[0].Value);
    }
    [Fact]
    public async Task DuplicateRequestsReplayAndChangedPayloadOrStaleVersionsFail()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner); var command = Command(item, warehouse, 10);
        var first = await Read<DocumentDto>(await Stock(owner, "documents", command));
        var repeat = await Read<DocumentDto>(await Stock(owner, "documents", command)); Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, "documents", command with { Quantity = 20 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, "documents", Command(item, warehouse, 1, null, "AdjustmentOut"))).StatusCode);
        var balance = Assert.Single(await Balances(owner, item)); Assert.Equal(10, balance.Quantity);
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, "documents", Command(item, warehouse, 11, null, "AdjustmentOut", balance.Sequence))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Stock(owner, "documents", Command(item, warehouse, 1, 999, "AdjustmentOut", balance.Sequence))).StatusCode);
        var reverse = new ReverseStockRequest(Guid.NewGuid().ToString(), "Opening was incorrect");
        var r1 = await Read<DocumentDto>(await Stock(owner, $"documents/{first.Id}/reverse", reverse));
        var r2 = await Read<DocumentDto>(await Stock(owner, $"documents/{first.Id}/reverse", reverse)); Assert.Equal(r1.Id, r2.Id);
        balance = Assert.Single(await Balances(owner, item)); Assert.Equal(0, balance.Quantity); Assert.Equal(0, balance.WarehouseValue);
    }
    [Fact]
    public async Task ConcurrentIssuesCannotOversellAndConcurrentRetryPostsOnce()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner);
        // Pre-establish antiforgery cookie before parallel commands.
        await client.GetAsync("/api/auth/csrf");
        var opening = Command(item, warehouse, 10);
        var copies = await Task.WhenAll(Stock(owner, "documents", opening), Stock(owner, "documents", opening));
        var docs = await Task.WhenAll(copies.Select(Read<DocumentDto>)); Assert.Equal(docs[0].Id, docs[1].Id);
        var seq = Assert.Single(await Balances(owner, item)).Sequence;
        var issues = await Task.WhenAll(Stock(owner, "documents", Command(item, warehouse, 7, null, "AdjustmentOut", seq)), Stock(owner, "documents", Command(item, warehouse, 7, null, "AdjustmentOut", seq)));
        Assert.Single(issues, x => x.IsSuccessStatusCode); Assert.Single(issues, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(3, Assert.Single(await Balances(owner, item)).Quantity);
    }
    [Fact]
    public async Task SerialsHaveOneLocationAndReversalRestoresCustody()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner, "Cylinder", true);
        await Read<AssetDto>(await Stock(owner, "assets", new AssetRequest(item.Id, "CYL-1", "Tenant owned", "INS-1", null)));
        var opening = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 1) with { Serials = ["CYL-1"] }));
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, "documents", Command(item, warehouse, 1, 100, "AdjustmentIn", opening.Movements[0].Sequence) with { Serials = ["CYL-1"] })).StatusCode);
        var state = Command(item, warehouse, 1, null, "Transfer", opening.Movements[0].Sequence) with { Serials = ["CYL-1"], DestinationWarehouseId = warehouse.Id, DestinationBucket = "Full", DestinationExpectedSequence = opening.Movements[0].Sequence };
        var changed = await Read<DocumentDto>(await Stock(owner, "documents", state));
        Assert.Equal(1, (await Balances(owner, item)).Single(x => x.Bucket == "Full").Quantity);
        await Read<DocumentDto>(await Stock(owner, $"documents/{changed.Id}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Wrong state")));
        var balances = await Balances(owner, item); Assert.Equal(1, balances.Single(x => x.Bucket == "Empty").Quantity); Assert.Equal(0, balances.Single(x => x.Bucket == "Full").Quantity);
        Assert.Equal(HttpStatusCode.BadRequest, (await Stock(owner, "documents", Command(item, warehouse, 0.5m, 100, "AdjustmentIn", balances[0].Sequence) with { Serials = ["CYL-1"] })).StatusCode);
    }
    [Fact]
    public async Task ForeignIdsCollectionsAndCompositeReferencesStayTenantScoped()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var (ia, wa) = await Setup(a); var (ib, wb) = await Setup(b);
        var document = await Read<DocumentDto>(await Stock(b, "documents", Command(ib, wb, 5)));
        foreach (var path in new[] { $"items/{ib.Id}", $"items/{ib.Id}/balances", $"items/{ib.Id}/ledger", $"items/{ib.Id}/assets", $"documents/{document.Id}" })
            Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, path)).StatusCode);
        foreach (var path in new[] { "units", "categories", "items", "warehouses" })
            Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, "/api/workspace/inventory/" + path, HttpMethod.Get, tenant: b.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, "documents", Command(ib, wa, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, "documents", Command(ia, wb, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, $"documents/{document.Id}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Foreign"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, "items", new ItemRequest("FOREIGN", "Foreign", "Lpg", ib.UnitId, null, "", "Domestic", 1, null, false, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, "warehouses", new WarehouseRequest(wb.BranchId, "Foreign", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, "assets", new AssetRequest(ib.Id, "FOREIGN", "Owner", "", null))).StatusCode);
        Assert.DoesNotContain((await Read<PageDto<ItemDto>>(await Stock(a, "items"))).Items, x => x.Id == ib.Id);
        Assert.DoesNotContain(await Read<WarehouseDto[]>(await Stock(a, "warehouses")), x => x.Id == wb.Id);
        var options = new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new Gas365DbContext(options, new TenantScope { TenantId = a.Me.TenantId });
        db.StockItems.Add(new StockItem { TenantId = a.Me.TenantId, Sku = "CROSS-FK", Name = "Invalid", UnitId = ib.UnitId });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task DatabaseRejectsPostedMutationAndPrecisionIsValidated()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await Stock(owner, "documents", Command(item, warehouse, 1.0001m))).StatusCode);
        var posted = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 1)));
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"StockMovements\" WHERE \"TenantId\" = {owner.Me.TenantId} AND \"DocumentId\" = {posted.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"StockDocuments\" SET \"Reason\" = 'tampered' WHERE \"TenantId\" = {owner.Me.TenantId} AND \"Id\" = {posted.Id}"));
    }

    [Fact]
    public async Task PermissionsAndBranchGrantsAreRecheckedForEveryStockAction()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner);
        var role = await Read<RoleDto>(await Send(client, "/api/workspace/roles", HttpMethod.Post,
            new RoleRequest("Stock worker", [Permissions.InventoryView, Permissions.InventoryPost, Permissions.InventoryReverse, Permissions.CatalogManage]), owner.Me.TenantId));
        var email = $"worker-{Guid.NewGuid():N}@test.example";
        (await Send(client, "/api/workspace/members", HttpMethod.Post, new InviteRequest(email, "Stock worker", role.Id, []), owner.Me.TenantId)).EnsureSuccessStatusCode();
        using var worker = Client();
        (await Send(worker, "/api/auth/reset", HttpMethod.Post, new ResetRequest(email, messages.Token(email), Password))).EnsureSuccessStatusCode();
        (await Send(worker, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        var me = (await worker.GetFromJsonAsync<MeDto>("/api/auth/me"))!; var actor = (worker, me);
        Assert.Empty(await Read<WarehouseDto[]>(await Stock(actor, "warehouses")));
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(actor, "documents", Command(item, warehouse, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(actor, "warehouses", new WarehouseRequest(warehouse.BranchId, "Forbidden", ""))).StatusCode);
        var posted = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 1)));
        Assert.Empty(await Balances(actor, item));
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(actor, $"documents/{posted.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(actor, $"documents/{posted.Id}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Hidden branch"))).StatusCode);
        (await Send(client, $"/api/workspace/roles/{role.Id}", HttpMethod.Put, new RoleRequest(role.Name, [], role.Version), owner.Me.TenantId)).EnsureSuccessStatusCode();
        foreach (var path in new[] { "items", "units", "categories", "warehouses", $"items/{item.Id}", $"items/{item.Id}/balances", $"items/{item.Id}/ledger", $"items/{item.Id}/assets", $"documents/{posted.Id}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Stock(actor, path)).StatusCode);
        foreach (var path in new[] { "units", "categories", "items", "warehouses", "assets", "documents", $"documents/{posted.Id}/reverse" })
        {
            object body = path switch
            {
                "units" => new UnitRequest("NO", 0),
                "categories" => new CategoryRequest("No"),
                "items" => new ItemRequest("NO", "No", "Lpg", item.UnitId, null, "", "Domestic", 1, null, false, 0),
                "warehouses" => new WarehouseRequest(warehouse.BranchId, "No", ""),
                "assets" => new AssetRequest(item.Id, "NO", "No", "", null),
                "documents" => Command(item, warehouse, 1),
                _ => new ReverseStockRequest(Guid.NewGuid().ToString(), "No")
            };
            Assert.Equal(HttpStatusCode.Forbidden, (await Stock(actor, path, body)).StatusCode);
        }
    }

    [Fact]
    public async Task CatalogUpdatesEnforceConcurrencyAndForeignIds()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var (item, warehouse) = await Setup(a); var (other, otherWarehouse) = await Setup(b);
        var edit = new ItemRequest(item.Sku, "Updated", item.Kind, item.UnitId, null, item.Brand, item.Classification, item.CapacityKg, item.TareWeightKg, false, 3, true, item.Version);
        await Read<ItemDto>(await Stock(a, $"items/{item.Id}", edit, HttpMethod.Put));
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(a, $"items/{item.Id}", edit, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, $"items/{other.Id}", edit, HttpMethod.Put)).StatusCode);
        var location = new WarehouseRequest(warehouse.BranchId, "Updated store", "Rack", true, warehouse.Version);
        await Read<WarehouseDto>(await Stock(a, $"warehouses/{warehouse.Id}", location, HttpMethod.Put));
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(a, $"warehouses/{warehouse.Id}", location, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, $"warehouses/{otherWarehouse.Id}", location, HttpMethod.Put)).StatusCode);
        var (cylinder, _) = await Setup(a, "Cylinder", true);
        var asset = await Read<AssetDto>(await Stock(a, "assets", new AssetRequest(cylinder.Id, "EDIT-1", "Owner", "", null)));
        var assetEdit = new AssetRequest(cylinder.Id, asset.Serial, "Owner", "Passed", new DateOnly(2027, 1, 1), asset.Version);
        await Read<AssetDto>(await Stock(a, $"assets/{asset.Id}", assetEdit, HttpMethod.Put));
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(a, $"assets/{asset.Id}", assetEdit, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, $"assets/{Guid.NewGuid()}", assetEdit, HttpMethod.Put)).StatusCode);
        var category = await Read<CategoryDto>(await Stock(b, "categories", new CategoryRequest("Foreign parts")));
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(a, "items", edit with { Sku = "FOREIGN-CATEGORY", Kind = "SparePart", CategoryId = category.Id })).StatusCode);
    }

    [Fact]
    public async Task EveryInventoryRouteRejectsForeignWorkspaceBeforeBindingOrWriting()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var id = Guid.NewGuid();
        var routes = new (string Path, HttpMethod Method)[] {
            ("units",HttpMethod.Get),("units",HttpMethod.Post),("categories",HttpMethod.Get),("categories",HttpMethod.Post),
            ("items",HttpMethod.Get),("items",HttpMethod.Post),($"items/{id}",HttpMethod.Get),($"items/{id}",HttpMethod.Put),
            ("warehouses",HttpMethod.Get),("warehouses",HttpMethod.Post),($"warehouses/{id}",HttpMethod.Put),
            ($"items/{id}/balances",HttpMethod.Get),($"items/{id}/ledger",HttpMethod.Get),($"items/{id}/assets",HttpMethod.Get),
            ("assets",HttpMethod.Post),($"assets/{id}",HttpMethod.Put),("documents",HttpMethod.Post),($"documents/{id}",HttpMethod.Get),($"documents/{id}/reverse",HttpMethod.Post)
        };
        foreach (var (path, method) in routes)
            Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, "/api/workspace/inventory/" + path, method, new { }, b.Me.TenantId)).StatusCode);
    }
    [Fact]
    public async Task CostPoolSpansStatesAndFailedTransferLeavesLedgerUnchanged()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner);
        var first = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 10, 100)));
        var second = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 10, 200, "AdjustmentIn", first.Movements[0].Sequence) with { Bucket = "Damaged" }));
        var balances = await Balances(owner, item); Assert.All(balances, b => { Assert.Equal(150, b.AverageCost); Assert.Equal(3000, b.WarehouseValue); });
        var invalid = Command(item, warehouse, 5, null, "Transfer", second.Movements[0].Sequence) with { DestinationWarehouseId = Guid.NewGuid(), DestinationBucket = "Available" };
        Assert.Equal(HttpStatusCode.NotFound, (await Stock(owner, "documents", invalid)).StatusCode);
        Assert.Equal(second.Movements[0].Sequence, (await Balances(owner, item))[0].Sequence);
        var issue = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 10, null, "AdjustmentOut", second.Movements[0].Sequence)));
        Assert.Equal(-1500, issue.Movements[0].Value);
        var last = await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 10, null, "AdjustmentOut", issue.Movements[0].Sequence) with { Bucket = "Damaged" }));
        Assert.Equal(-1500, last.Movements[0].Value);
        Assert.All(await Balances(owner, item), b => { Assert.Equal(0, b.Quantity); Assert.Equal(0, b.WarehouseValue); });
    }
}
