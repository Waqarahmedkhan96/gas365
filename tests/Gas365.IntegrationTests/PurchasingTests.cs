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

public sealed class PurchasingTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IAsyncLifetime
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

    private static Task<HttpResponseMessage> Purchase((HttpClient Client, MeDto Me) owner, string path, object? body = null, HttpMethod? method = null) =>
        Send(owner.Client, "/api/workspace/purchasing/" + path, method ?? (body is null ? HttpMethod.Get : HttpMethod.Post), body, owner.Me.TenantId);
    private static async Task<(PurchaseDto Draft, PurchaseDraftRequest Request, ItemDto Item, WarehouseDto Warehouse)> Draft((HttpClient Client, MeDto Me) owner)
    {
        var (item, warehouse) = await Setup(owner);
        var supplier = await Read<SupplierDto>(await Purchase(owner, "suppliers", new SupplierRequest("Synthetic supplier", "0300-0000000", "Test address")));
        var request = new PurchaseDraftRequest(warehouse.BranchId, supplier.Id, "SUP-001", "Synthetic purchase", "Value", 60,
            [new(item.Id, warehouse.Id, "Available", 10, 100, 10, []), new(item.Id, warehouse.Id, "Damaged", 20, 200, 20, [])]);
        return (await Read<PurchaseDto>(await Purchase(owner, "purchases", request)), request, item, warehouse);
    }
    private static PurchasePostRequest Post(PurchaseDto draft, decimal paid = 0) => new(Guid.NewGuid().ToString(), draft.Version, paid);
    private static PurchaseReverseRequest Reverse() => new(Guid.NewGuid().ToString(), "Correction with cash returned");

    [Fact]
    public async Task PostingPaymentsReversalAndReplacementReconcileAllLedgers()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, request, item, warehouse) = await Draft(owner);
        Assert.Equal(5060, draft.Total); Assert.Equal(12, draft.Snapshot.Lines[0].LandedCost);
        var post = Post(draft, 1000); var purchase = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}/post", post));
        Assert.Equal("Partially Paid", purchase.Status); Assert.Equal(4060, purchase.Outstanding);
        var replay = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}/post", post)); Assert.Equal(purchase.Id, replay.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase(owner, $"purchases/{draft.Id}/post", post with { CashPaid = 0 })).StatusCode);
        var balances = await Balances(owner, item); Assert.Equal(30, balances.Sum(x => x.Quantity)); Assert.All(balances, x => Assert.Equal(5060, x.WarehouseValue));
        var payment = new PurchasePaymentRequest(Guid.NewGuid().ToString(), 60, "Partial cash", purchase.Entries[^1].Id);
        purchase = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{purchase.Id}/payments", payment)); Assert.Equal(4000, purchase.Outstanding);
        var retry = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{purchase.Id}/payments", payment)); Assert.Equal(3, retry.Entries.Length);
        var profile = await Read<SupplierProfileDto>(await Purchase(owner, $"suppliers/{purchase.SupplierId}?branchId={warehouse.BranchId}")); Assert.Equal(4000, profile.Balance);
        var voucher = await Purchase(owner, $"purchases/{purchase.Id}/voucher"); Assert.Equal("application/pdf", voucher.Content.Headers.ContentType!.MediaType); Assert.StartsWith("%PDF-", await voucher.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase(owner, $"purchases/{draft.Id}", request with { Version = draft.Version }, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, $"documents/{purchase.StockDocumentId}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Bypass"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase(owner, $"purchases/{purchase.Id}/reverse", Reverse())).StatusCode);
        foreach (var entry in purchase.Entries.Where(x => x.Kind == "Payment"))
        {
            var reversal = Reverse();
            await Read<PurchaseDto>(await Purchase(owner, $"purchases/{purchase.Id}/payments/{entry.Id}/reverse", reversal));
            await Read<PurchaseDto>(await Purchase(owner, $"purchases/{purchase.Id}/payments/{entry.Id}/reverse", reversal));
        }
        var reverse = Reverse(); purchase = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{purchase.Id}/reverse", reverse));
        await Read<PurchaseDto>(await Purchase(owner, $"purchases/{purchase.Id}/reverse", reverse));
        Assert.Equal("Reversed", purchase.Status); Assert.Equal(0, purchase.Outstanding); Assert.Equal(0, purchase.Paid);
        Assert.All(await Balances(owner, item), x => { Assert.Equal(0, x.Quantity); Assert.Equal(0, x.WarehouseValue); });
        var replacement = await Read<PurchaseDto>(await Purchase(owner, "purchases", request with { CorrectsId = purchase.Id, Reference = "SUP-CORRECTION" }));
        replacement = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{replacement.Id}/post", Post(replacement)));
        Assert.Equal(purchase.Id, replacement.CorrectsId); Assert.Equal(5060, replacement.Total);
    }
    [Fact]
    public async Task ConcurrentPostRetriesAndPaymentsCannotDuplicateOrOverpay()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, _, item, _) = await Draft(owner); var post = Post(draft);
        var responses = await Task.WhenAll(Purchase(owner, $"purchases/{draft.Id}/post", post), Purchase(owner, $"purchases/{draft.Id}/post", post));
        var docs = await Task.WhenAll(responses.Select(Read<PurchaseDto>)); Assert.Equal(docs[0].Id, docs[1].Id);
        var p = docs[0]; var payment = new PurchasePaymentRequest(Guid.NewGuid().ToString(), 4000, "Concurrent cash", p.Entries[^1].Id);
        var pays = await Task.WhenAll(Purchase(owner, $"purchases/{p.Id}/payments", payment), Purchase(owner, $"purchases/{p.Id}/payments", payment with { IdempotencyKey = Guid.NewGuid().ToString() }));
        Assert.Single(pays, x => x.IsSuccessStatusCode); Assert.Single(pays, x => x.StatusCode == HttpStatusCode.Conflict);
        p = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{p.Id}")); Assert.Equal(1060, p.Outstanding);
        Assert.Equal(HttpStatusCode.BadRequest, (await Purchase(owner, $"purchases/{p.Id}/payments", payment with { IdempotencyKey = Guid.NewGuid().ToString(), ExpectedLastEntryId = p.Entries[^1].Id })).StatusCode);
        Assert.Equal(30, (await Balances(owner, item)).Sum(x => x.Quantity));
    }
    [Fact]
    public async Task DraftConcurrencyAndFailedPostingLeaveNoEffects()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, request, item, warehouse) = await Draft(owner);
        var edits = await Task.WhenAll(Purchase(owner, $"purchases/{draft.Id}", request with { Version = draft.Version, Notes = "A" }, HttpMethod.Put), Purchase(owner, $"purchases/{draft.Id}", request with { Version = draft.Version, Notes = "B" }, HttpMethod.Put));
        Assert.Single(edits, x => x.IsSuccessStatusCode); Assert.Single(edits, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase(owner, $"purchases/{draft.Id}/post", Post(draft))).StatusCode);
        draft = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Purchase(owner, $"purchases/{draft.Id}/post", Post(draft, 6000))).StatusCode);
        Assert.Empty(await Balances(owner, item));
        var (serialItem, _) = await Setup(owner, "Cylinder", true);
        var invalid = await Read<PurchaseDto>(await Purchase(owner, "purchases", request with { Lines = [request.Lines[0], new(serialItem.Id, warehouse.Id, "Empty", 1, 100, 1, ["MISSING"])] }));
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(owner, $"purchases/{invalid.Id}/post", Post(invalid))).StatusCode);
        Assert.Empty(await Balances(owner, item));
        var profile = await Read<SupplierProfileDto>(await Purchase(owner, $"suppliers/{draft.SupplierId}?branchId={warehouse.BranchId}")); Assert.Equal(0, profile.Balance); Assert.Empty(profile.Ledger.Items);
    }
    [Fact]
    public async Task LaterStockBlocksReversalAndDatabaseRejectsPostedMutation()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, _, item, warehouse) = await Draft(owner);
        var p = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}/post", Post(draft)));
        var balance = (await Balances(owner, item))[0];
        await Read<DocumentDto>(await Stock(owner, "documents", Command(item, warehouse, 1, null, "AdjustmentOut", balance.Sequence)));
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase(owner, $"purchases/{p.Id}/reverse", Reverse())).StatusCode);
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"PurchaseEntries\" WHERE \"PurchaseId\" = {p.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PurchaseDocuments\" SET \"Total\" = 0 WHERE \"Id\" = {p.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PurchaseDrafts\" SET \"Content\" = '{{}}' WHERE \"Id\" = {draft.Id}"));
    }
    [Fact]
    public async Task SupplierVersionsAndForeignIdsAreRejected()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var (da, ra, _, wa) = await Draft(a); var (db, rb, _, wb) = await Draft(b);
        var pb = await Read<PurchaseDto>(await Purchase(b, $"purchases/{db.Id}/post", Post(db, 10)));
        foreach (var path in new[] { $"suppliers/{db.SupplierId}?branchId={wa.BranchId}", $"suppliers/{da.SupplierId}?branchId={wb.BranchId}", $"purchases/{db.Id}", $"purchases/{pb.Id}/voucher", $"purchases?branchId={wb.BranchId}" })
            Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, "purchases", ra with { SupplierId = rb.SupplierId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, "purchases", ra with { Lines = rb.Lines })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, $"purchases/{db.Id}", ra, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, $"purchases/{db.Id}/post", Post(db))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, $"purchases/{pb.Id}/reverse", Reverse())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, $"purchases/{pb.Id}/payments", new PurchasePaymentRequest(Guid.NewGuid().ToString(), 1, "Foreign", pb.Entries[^1].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, $"purchases/{pb.Id}/payments/{pb.Entries[^1].Id}/reverse", Reverse())).StatusCode);
        var supplier = (await Read<PageDto<SupplierDto>>(await Purchase(a, "suppliers"))).Items[0];
        var update = new SupplierRequest("Updated supplier", "", "", true, supplier.Version);
        await Read<SupplierDto>(await Purchase(a, $"suppliers/{supplier.Id}", update, HttpMethod.Put));
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase(a, $"suppliers/{supplier.Id}", update, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Purchase(a, $"suppliers/{rb.SupplierId}", update, HttpMethod.Put)).StatusCode);
        Assert.DoesNotContain((await Read<PageDto<SupplierDto>>(await Purchase(a, "suppliers"))).Items, x => x.Id == rb.SupplierId);
        var options = new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var context = new Gas365DbContext(options, new TenantScope { TenantId = a.Me.TenantId });
        context.PurchaseDrafts.Add(new PurchaseDraft { TenantId = a.Me.TenantId, BranchId = wa.BranchId, SupplierId = rb.SupplierId, Content = "{}", Snapshot = "{}" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
    [Fact]
    public async Task EveryRouteEnforcesWorkspacePermissionAndBranchGrants()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, request, _, warehouse) = await Draft(owner);
        var p = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}/post", Post(draft, 10)));
        var role = await Read<RoleDto>(await Send(client, "/api/workspace/roles", HttpMethod.Post, new RoleRequest("Purchaser", [Permissions.PurchaseView, Permissions.PurchaseDraft, Permissions.PurchasePost, Permissions.PurchasePay, Permissions.PurchaseReverse, Permissions.SupplierManage]), owner.Me.TenantId));
        var email = $"purchaser-{Guid.NewGuid():N}@test.example";
        (await Send(client, "/api/workspace/members", HttpMethod.Post, new InviteRequest(email, "Purchaser", role.Id, []), owner.Me.TenantId)).EnsureSuccessStatusCode();
        using var worker = Client(); (await Send(worker, "/api/auth/reset", HttpMethod.Post, new ResetRequest(email, messages.Token(email), Password))).EnsureSuccessStatusCode();
        (await Send(worker, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        var actor = (worker, (await worker.GetFromJsonAsync<MeDto>("/api/auth/me"))!);
        var routes = new (string Path, HttpMethod Method, object? Body)[] {
            ("suppliers", HttpMethod.Get, null), ("suppliers", HttpMethod.Post, new SupplierRequest("Denied", "", "")),
            ($"suppliers/{draft.SupplierId}", HttpMethod.Put, new SupplierRequest("Denied", "", "")),
            ($"suppliers/{draft.SupplierId}?branchId={warehouse.BranchId}", HttpMethod.Get, null),
            ($"purchases?branchId={warehouse.BranchId}", HttpMethod.Get, null), ($"purchases/{p.Id}", HttpMethod.Get, null),
            ($"purchases/{p.Id}/voucher", HttpMethod.Get, null), ("purchases", HttpMethod.Post, request),
            ($"purchases/{draft.Id}", HttpMethod.Put, request), ($"purchases/{draft.Id}/post", HttpMethod.Post, Post(draft)),
            ($"purchases/{p.Id}/payments", HttpMethod.Post, new PurchasePaymentRequest(Guid.NewGuid().ToString(), 1, "Denied", p.Entries[^1].Id)),
            ($"purchases/{p.Id}/payments/{p.Entries[^1].Id}/reverse", HttpMethod.Post, Reverse()), ($"purchases/{p.Id}/reverse", HttpMethod.Post, Reverse()) };
        foreach (var route in routes)
        {
            Assert.Equal(HttpStatusCode.Conflict, (await Send(client, "/api/workspace/purchasing/" + route.Path, route.Method, route.Body, Guid.NewGuid())).StatusCode);
            if (route.Path.StartsWith("purchases", StringComparison.Ordinal) || route.Path.Contains("branchId", StringComparison.Ordinal))
                Assert.Equal(HttpStatusCode.NotFound, (await Purchase(actor, route.Path, route.Body, route.Method)).StatusCode);
        }
        (await Send(client, $"/api/workspace/roles/{role.Id}", HttpMethod.Put, new RoleRequest(role.Name, [], role.Version), owner.Me.TenantId)).EnsureSuccessStatusCode();
        foreach (var route in routes) Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, route.Path, route.Body, route.Method)).StatusCode);
    }
    [Fact]
    public async Task ViewPermissionDoesNotGrantFinancialCommands()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, request, _, warehouse) = await Draft(owner);
        var role = await Read<RoleDto>(await Send(client, "/api/workspace/roles", HttpMethod.Post, new RoleRequest("Purchase reader", [Permissions.PurchaseView]), owner.Me.TenantId));
        var email = $"reader-{Guid.NewGuid():N}@test.example";
        (await Send(client, "/api/workspace/members", HttpMethod.Post, new InviteRequest(email, "Reader", role.Id, [warehouse.BranchId]), owner.Me.TenantId)).EnsureSuccessStatusCode();
        using var worker = Client(); (await Send(worker, "/api/auth/reset", HttpMethod.Post, new ResetRequest(email, messages.Token(email), Password))).EnsureSuccessStatusCode();
        (await Send(worker, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        var actor = (worker, (await worker.GetFromJsonAsync<MeDto>("/api/auth/me"))!);
        await Read<PurchaseDto>(await Purchase(actor, $"purchases/{draft.Id}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, "suppliers", new SupplierRequest("No", "", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, "purchases", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, $"purchases/{draft.Id}/post", Post(draft))).StatusCode);
        var p = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}/post", Post(draft, 1)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, $"purchases/{p.Id}/payments", new PurchasePaymentRequest(Guid.NewGuid().ToString(), 1, "No", p.Entries[^1].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, $"purchases/{p.Id}/reverse", Reverse())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Purchase(actor, $"purchases/{p.Id}/payments/{p.Entries[^1].Id}/reverse", Reverse())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Purchase(actor, $"purchases/{p.Id}/voucher")).StatusCode);
    }
    [Fact]
    public async Task ConcurrentDraftEditAndPostingUseOneConsistentSnapshot()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, request, _, _) = await Draft(owner);
        var responses = await Task.WhenAll(Purchase(owner, $"purchases/{draft.Id}", request with { Version = draft.Version, LandedCharges = 90 }, HttpMethod.Put), Purchase(owner, $"purchases/{draft.Id}/post", Post(draft)));
        Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var result = await Read<PurchaseDto>(await Purchase(owner, $"purchases/{draft.Id}"));
        Assert.Equal(result.Status == "Draft" ? 5090 : 5060, result.Total);
    }
}
