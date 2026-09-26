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

public sealed partial class CustomerTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IAsyncLifetime
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
    private static async Task<T> Read<T>(HttpResponseMessage response) { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<T>())!; }
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

    private static Task<HttpResponseMessage> Sales((HttpClient Client, MeDto Me) owner, string path, object? body = null, HttpMethod? method = null) =>
        Send(owner.Client, "/api/workspace/sales/" + path, method ?? (body is null ? HttpMethod.Get : HttpMethod.Post), body, owner.Me.TenantId);

    [Fact]
    public async Task SalesPostingPaymentsFillingLossAndReversalReconcile()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, warehouse) = await Setup(owner);
        await Read<DocumentDto>(await Stock(owner, "documents", new PostStockRequest(Guid.NewGuid().ToString(), "Opening", item.Id, warehouse.Id, "Available", 10, 100, null, null, "Opening", 0, 0, [])));
        var customer = await Read<CustomerDto>(await Sales(owner, "customers", new CustomerRequest("Cash customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(owner, "price-lists", new PriceListRequest("Retail")));
        var price = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{list.Id}/prices", new SalePriceRequest(item.Id, "Refill", 200)));
        var request = new SaleDraftRequest(warehouse.BranchId, customer.Id, list.Id, "Filling 1", "", [new(price.Id, price.Version, warehouse.Id, 1.6m, 0, "Customer cylinder A", 0.1m, "Filling loss")]);
        var draft = await Read<SaleDraftDto>(await Sales(owner, "drafts", request));
        var post = new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 100, [[]]);
        var sale = await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", post));
        Assert.Equal(320, sale.Total); Assert.Equal(170, sale.Cogs); Assert.Equal(150, sale.GrossProfit); Assert.Equal(220, sale.Outstanding);
        Assert.Equal(sale.Id, (await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", post))).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"drafts/{draft.Id}/post", post with { CashPaid = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"drafts/{draft.Id}", request with { Version = draft.Version }, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Stock(owner, $"documents/{sale.StockDocumentId}/reverse", new ReverseStockRequest(Guid.NewGuid().ToString(), "Wrong route"))).StatusCode);
        var balances = await Read<StockBalanceDto[]>(await Stock(owner, $"items/{item.Id}/balances"));
        Assert.Equal(8.3m, balances.Single().Quantity);
        var payment = new SalePaymentRequest(Guid.NewGuid().ToString(), 220, "Balance received", sale.Entries[^1].Id);
        sale = await Read<SaleDto>(await Sales(owner, $"posted/{sale.Id}/payments", payment));
        Assert.Equal(0, sale.Outstanding); Assert.Equal(320, sale.Paid);
        Assert.Equal(3, (await Read<SaleDto>(await Sales(owner, $"posted/{sale.Id}/payments", payment))).Entries.Length);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Correction"))).StatusCode);
        foreach (var entry in sale.Entries.Where(x => x.Kind == "Payment"))
            await Read<SaleDto>(await Sales(owner, $"posted/{sale.Id}/payments/{entry.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Cash returned")));
        sale = await Read<SaleDto>(await Sales(owner, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Correct filling")));
        Assert.Equal("Reversed", sale.Status); Assert.Equal(0, sale.Outstanding); Assert.Equal(0, sale.Entries.Sum(x => x.Cogs));
        Assert.Equal(10, (await Read<StockBalanceDto[]>(await Stock(owner, $"items/{item.Id}/balances"))).Single().Quantity);
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SaleDocuments\" SET \"Total\" = 1 WHERE \"Id\" = {sale.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"SaleEntries\" WHERE \"SaleId\" = {sale.Id}"));
    }

    [Fact]
    public async Task SalesLateFailureRollsBackAndForeignRoutesAreHidden()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var (item, warehouse) = await Setup(a);
        await Read<DocumentDto>(await Stock(a, "documents", new PostStockRequest(Guid.NewGuid().ToString(), "Opening", item.Id, warehouse.Id, "Available", 2, 100, null, null, "Opening", 0, 0, [])));
        var customer = await Read<CustomerDto>(await Sales(a, "customers", new CustomerRequest("Customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(a, "price-lists", new PriceListRequest("Retail")));
        var price = await Read<SalePriceDto>(await Sales(a, $"price-lists/{list.Id}/prices", new SalePriceRequest(item.Id, "Refill", 200)));
        var request = new SaleDraftRequest(warehouse.BranchId, customer.Id, list.Id, "Multi", "", [new(price.Id, price.Version, warehouse.Id, 1, 0), new(price.Id, price.Version, warehouse.Id, 2, 0)]);
        var draft = await Read<SaleDraftDto>(await Sales(a, "drafts", request));
        var post = new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 0, [[], []]);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(a, $"drafts/{draft.Id}/post", post)).StatusCode);
        Assert.Equal(2, (await Read<StockBalanceDto[]>(await Stock(a, $"items/{item.Id}/balances"))).Single().Quantity);
        Assert.Empty((await Read<PageDto<SaleDto>>(await Sales(a, $"posted?branchId={warehouse.BranchId}"))).Items);
        draft = await Read<SaleDraftDto>(await Sales(a, $"drafts/{draft.Id}", request with { Version = draft.Version, Lines = [request.Lines[0]] }, HttpMethod.Put));
        post = post with { Version = draft.Version, Serials = [[]] };
        var results = await Task.WhenAll(Sales(a, $"drafts/{draft.Id}/post", post), Sales(a, $"drafts/{draft.Id}/post", post));
        var sale = await Read<SaleDto>(results[0]); Assert.Equal(sale.Id, (await Read<SaleDto>(results[1])).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"posted/{sale.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"posted?branchId={warehouse.BranchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"drafts/{draft.Id}/post", post)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"posted/{sale.Id}/payments", new SalePaymentRequest(Guid.NewGuid().ToString(), 1, "Payment", sale.Entries[^1].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Reverse"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"posted/{sale.Id}/payments/{sale.Entries[0].Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Reverse"))).StatusCode);
    }

    [Fact]
    public async Task CustomersHaveValidatedSearchableProfilesAndConcurrencyProtection()
    {
        var owner = await Owner(); using var client = owner.Client;
        var r = new CustomerRequest("North customer", "03001234567", "Test address");
        var customer = await Read<CustomerDto>(await Sales(owner, "customers", r));
        Assert.Equal(customer, await Read<CustomerDto>(await Sales(owner, $"customers/{customer.Id}")));
        var found = await Read<PageDto<CustomerDto>>(await Sales(owner, "customers?search=north"));
        Assert.Single(found.Items); Assert.Equal(customer.Id, found.Items[0].Id);
        Assert.Empty((await Read<PageDto<CustomerDto>>(await Sales(owner, "customers?search=absent"))).Items);
        var updated = await Read<CustomerDto>(await Sales(owner, $"customers/{customer.Id}", r with { Name = "Updated", Active = false, Version = customer.Version }, HttpMethod.Put));
        Assert.False(updated.Active); Assert.NotEqual(customer.Version, updated.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"customers/{customer.Id}", r with { Version = customer.Version }, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(owner, "customers", r with { Name = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(owner, "customers?page=0")).StatusCode);
        await using var db = database.CreateContext();
        var audit = await db.AuditEvents.IgnoreQueryFilters().Where(x => x.TenantId == owner.Me.TenantId && x.SubjectId == customer.Id).ToArrayAsync();
        Assert.Equal(2, audit.Length); Assert.Contains(audit, x => x.Action == "customer.updated");
    }

    [Fact]
    public async Task PricesValidateModePrecisionDuplicatesAndStaleEdits()
    {
        var owner = await Owner(); using var client = owner.Client; var (item, _) = await Setup(owner);
        var list = await Read<PriceListDto>(await Sales(owner, "price-lists", new PriceListRequest("Retail")));
        var request = new SalePriceRequest(item.Id, "Refill", 123.456789m);
        var price = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{list.Id}/prices", request));
        Assert.Equal(request.UnitPrice, price.UnitPrice);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"price-lists/{list.Id}/prices", request)).StatusCode);
        var path = $"price-lists/{list.Id}/prices/{price.Id}";
        var updated = await Read<SalePriceDto>(await Sales(owner, path, request with { UnitPrice = 125, Version = price.Version }, HttpMethod.Put));
        Assert.Equal(125, updated.UnitPrice);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, path, request with { Version = price.Version }, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(owner, $"price-lists/{list.Id}/prices", request with { Mode = "Exchange" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(owner, $"price-lists/{list.Id}/prices", request with { UnitPrice = 1.1234567m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(owner, $"price-lists/{list.Id}/prices", request with { UnitPrice = -1 })).StatusCode);
        var rows = await Read<PageDto<SalePriceDto>>(await Sales(owner, $"price-lists/{list.Id}/prices")); Assert.Single(rows.Items);
        await Read<PriceListDto>(await Sales(owner, $"price-lists/{list.Id}", new PriceListRequest("Retail", false, list.Version), HttpMethod.Put));
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(owner, path, request with { Version = updated.Version }, HttpMethod.Put)).StatusCode);
    }

    [Fact]
    public async Task EveryCustomerAndPriceRouteRejectsForeignWorkspacesAndReferences()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var customer = await Read<CustomerDto>(await Sales(a, "customers", new CustomerRequest("Private", "", "")));
        var list = await Read<PriceListDto>(await Sales(a, "price-lists", new PriceListRequest("Private")));
        var (item, _) = await Setup(a);
        var price = await Read<SalePriceDto>(await Sales(a, $"price-lists/{list.Id}/prices", new SalePriceRequest(item.Id, "Refill", 10)));
        var routes = new (string Path, HttpMethod Method, object? Body)[] {
            ("customers", HttpMethod.Get, null), ($"customers/{customer.Id}", HttpMethod.Get, null),
            ("customers", HttpMethod.Post, new CustomerRequest("Test", "", "")),
            ($"customers/{customer.Id}", HttpMethod.Put, new CustomerRequest("Test", "", "", true, customer.Version)),
            ("price-lists", HttpMethod.Get, null), ("price-lists", HttpMethod.Post, new PriceListRequest("Test")),
            ($"price-lists/{list.Id}", HttpMethod.Put, new PriceListRequest("Test", true, list.Version)),
            ($"price-lists/{list.Id}/prices", HttpMethod.Get, null),
            ($"price-lists/{list.Id}/prices", HttpMethod.Post, new SalePriceRequest(item.Id, "Refill", 10)),
            ($"price-lists/{list.Id}/prices/{price.Id}", HttpMethod.Put, new SalePriceRequest(item.Id, "Refill", 10, true, price.Version)) };
        foreach (var route in routes)
            Assert.Equal(HttpStatusCode.Conflict, (await Send(cb, "/api/workspace/sales/" + route.Path, route.Method, route.Body, a.Me.TenantId)).StatusCode);
        Assert.Empty((await Read<PageDto<CustomerDto>>(await Sales(b, "customers"))).Items);
        Assert.Empty((await Read<PageDto<PriceListDto>>(await Sales(b, "price-lists"))).Items);
        foreach (var route in routes.Where(x => x.Path.Contains('/')))
            Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, route.Path, route.Body, route.Method)).StatusCode);
        var ownList = await Read<PriceListDto>(await Sales(b, "price-lists", new PriceListRequest("Own")));
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"price-lists/{ownList.Id}/prices", new SalePriceRequest(item.Id, "Refill", 10))).StatusCode);
        await using var db = database.CreateContext();
        var foreign = new SalePrice { TenantId = b.Me.TenantId, PriceListId = ownList.Id, ItemId = item.Id, Mode = "Refill", UnitPrice = 10 };
        var options = new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var scoped = new Gas365DbContext(options, new TenantScope { TenantId = b.Me.TenantId });
        scoped.SalePrices.Add(foreign); await Assert.ThrowsAsync<DbUpdateException>(() => scoped.SaveChangesAsync());
    }

    [Fact]
    public async Task LivePermissionChangesBlockReadsAndWrites()
    {
        var owner = await Owner(); using var client = owner.Client;
        var customer = await Read<CustomerDto>(await Sales(owner, "customers", new CustomerRequest("Customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(owner, "price-lists", new PriceListRequest("Read only")));
        var (item, _) = await Setup(owner);
        var priceRequest = new SalePriceRequest(item.Id, "Refill", 10);
        var price = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{list.Id}/prices", priceRequest));
        await using var db = database.CreateContext();
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == owner.Me.TenantId && x.IsOwner);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {new[] { Permissions.CustomerView }} WHERE \"TenantId\" = {owner.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.OK, (await Sales(owner, "customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, "customers", new CustomerRequest("Denied", "", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, $"customers/{customer.Id}", new CustomerRequest("Denied", "", "", true, customer.Version), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, "price-lists", new PriceListRequest("Denied"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Sales(owner, $"price-lists/{list.Id}/prices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, $"price-lists/{list.Id}", new PriceListRequest("Denied", true, list.Version), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, $"price-lists/{list.Id}/prices", priceRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, $"price-lists/{list.Id}/prices/{price.Id}", priceRequest with { Version = price.Version }, HttpMethod.Put)).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {Array.Empty<string>()} WHERE \"TenantId\" = {owner.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, "customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, $"customers/{customer.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, "price-lists")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(owner, $"price-lists/{list.Id}/prices")).StatusCode);
    }

    private static async Task<(SaleDraftRequest Request, SalePriceDto Price)> SaleRequest((HttpClient Client, MeDto Me) owner)
    {
        var (item, warehouse) = await Setup(owner);
        var customer = await Read<CustomerDto>(await Sales(owner, "customers", new CustomerRequest("Sales customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(owner, "price-lists", new PriceListRequest("Sales prices")));
        var price = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{list.Id}/prices", new SalePriceRequest(item.Id, "Refill", 123.456789m)));
        return (new(warehouse.BranchId, customer.Id, list.Id, "SALE-001", "Review", [new(price.Id, price.Version, warehouse.Id, 3.125m, 5.80m), new(price.Id, price.Version, warehouse.Id, 1, 0)]), price);
    }
    [Fact]
    public async Task SaleDraftsUseServerPricesPreserveSnapshotsAndHaveNoFinancialEffects()
    {
        var owner = await Owner(); using var client = owner.Client; var (request, price) = await SaleRequest(owner);
        var draft = await Read<SaleDraftDto>(await Sales(owner, "drafts", request));
        Assert.Equal(509.26m, draft.Snapshot.Gross); Assert.Equal(503.46m, draft.Snapshot.Net);
        Assert.Equal(123.456789m, draft.Snapshot.Lines[0].UnitPrice);
        var changedPrice = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{request.PriceListId}/prices/{price.Id}", new SalePriceRequest(price.ItemId, price.Mode, 200, true, price.Version), HttpMethod.Put));
        Assert.Equal(503.46m, (await Read<SaleDraftDto>(await Sales(owner, $"drafts/{draft.Id}"))).Snapshot.Net);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"drafts/{draft.Id}", request with { Version = draft.Version }, HttpMethod.Put)).StatusCode);
        var refresh = request with { Version = draft.Version, Lines = request.Lines.Select(x => x with { PriceVersion = changedPrice.Version }).ToArray() };
        var updated = await Read<SaleDraftDto>(await Sales(owner, $"drafts/{draft.Id}", refresh, HttpMethod.Put));
        Assert.Equal(819.20m, updated.Snapshot.Net);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"drafts/{draft.Id}", refresh, HttpMethod.Put)).StatusCode);
        Assert.Single((await Read<PageDto<SaleDraftDto>>(await Sales(owner, $"drafts?branchId={request.BranchId}"))).Items);
        await using var db = database.CreateContext();
        Assert.False(await db.StockDocuments.IgnoreQueryFilters().AnyAsync(x => x.TenantId == owner.Me.TenantId));
        Assert.False(await db.PurchaseEntries.IgnoreQueryFilters().AnyAsync(x => x.TenantId == owner.Me.TenantId));
        Assert.Equal(2, await db.AuditEvents.IgnoreQueryFilters().CountAsync(x => x.TenantId == owner.Me.TenantId && x.Action == "sale.draft_saved"));
    }
    [Fact]
    public async Task SalesDraftsRejectForeignTenantsBranchesAndInvalidInputs()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var (r, _) = await SaleRequest(a); var (other, _) = await SaleRequest(b);
        var draft = await Read<SaleDraftDto>(await Sales(a, "drafts", r));
        foreach (var route in new[] { ($"drafts?branchId={r.BranchId}", HttpMethod.Get), ($"drafts/{draft.Id}", HttpMethod.Get), ("drafts", HttpMethod.Post), ($"drafts/{draft.Id}", HttpMethod.Put) })
            Assert.Equal(HttpStatusCode.Conflict, (await Send(cb, "/api/workspace/sales/" + route.Item1, route.Item2, r, a.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"drafts/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"drafts/{draft.Id}", other with { Version = draft.Version }, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, $"drafts?branchId={r.BranchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, "drafts", other with { CustomerId = r.CustomerId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, "drafts", other with { PriceListId = r.PriceListId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(b, "drafts", other with { Lines = r.Lines })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(a, "drafts", r with { Lines = [r.Lines[0] with { WarehouseId = other.Lines[0].WarehouseId }] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(a, "drafts", r with { Lines = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(a, "drafts", r with { Lines = [r.Lines[0] with { Quantity = 0.0001m }] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sales(a, "drafts", r with { Lines = [r.Lines[0] with { Discount = 1000 }] })).StatusCode);
        await using var db = database.CreateContext();
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == a.Me.TenantId && x.IsOwner);
        var allowed = Permissions.All.Where(x => x != Permissions.SalesDiscount).ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {allowed} WHERE \"TenantId\" = {a.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(a, "drafts", r)).StatusCode);
        allowed = [Permissions.CustomerView];
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {allowed} WHERE \"TenantId\" = {a.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(a, "drafts", r)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(a, $"drafts/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(a, $"drafts?branchId={r.BranchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(a, $"drafts/{draft.Id}", r with { Version = draft.Version }, HttpMethod.Put)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentDraftEditsHaveOneWinnerAndBranchGrantsAreEnforced()
    {
        var owner = await Owner(); using var client = owner.Client; var (r, _) = await SaleRequest(owner);
        var draft = await Read<SaleDraftDto>(await Sales(owner, "drafts", r));
        // Obtain CSRF before parallel writes so the test exercises the draft version, not token setup.
        var csrf = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/auth/csrf"))!["token"];
        async Task<HttpResponseMessage> Edit(string reference)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/workspace/sales/drafts/{draft.Id}");
            request.Headers.Add("X-Workspace", owner.Me.TenantId.ToString()); request.Headers.Add("X-CSRF-TOKEN", csrf);
            request.Content = JsonContent.Create(r with { Reference = reference, Version = draft.Version });
            return await client.SendAsync(request);
        }
        var results = await Task.WhenAll(Edit("ONE"), Edit("TWO"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var otherBranch = await Read<BranchDto>(await Send(client, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Restricted branch"), owner.Me.TenantId));
        await using var db = database.CreateContext();
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == owner.Me.TenantId && x.IsOwner);
        var allowed = new[] { Permissions.SalesView, Permissions.SalesDraft, Permissions.SalesDiscount };
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {allowed}, \"IsOwner\" = FALSE WHERE \"TenantId\" = {owner.Me.TenantId} AND \"Id\" = {role.Id}");
        // No branch grant exists for the newly added branch.
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(owner, $"drafts?branchId={otherBranch.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Sales(owner, "drafts", r with { BranchId = otherBranch.Id })).StatusCode);
    }
}
