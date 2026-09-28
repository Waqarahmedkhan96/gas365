using System.Net;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    [Fact]
    public async Task SupplierAdvanceAndSeparatePurchaseCostsReconcileWithDrawer()
    {
        var o = await Owner(); using var client = o.Client; var (item, warehouse) = await Setup(o); var branch = warehouse.BranchId;
        Task<HttpResponseMessage> Purchase(string path, object? body = null) => Send(client, "/api/workspace/purchasing/" + path, body is null ? HttpMethod.Get : HttpMethod.Post, body, o.Me.TenantId);
        var supplier = await Read<SupplierDto>(await Purchase("suppliers", new SupplierRequest("Goods vendor", "", "")));
        var draft = await Read<PurchaseDto>(await Purchase("purchases", new PurchaseDraftRequest(branch, supplier.Id, "Finance purchase", "", "Quantity", 25, [new(item.Id, warehouse.Id, "Available", 2, 100, 0, [])])));
        var doc = await Read<PurchaseDto>(await Purchase($"purchases/{draft.Id}/post", new PurchasePostRequest(Key(), draft.Version, 0)));
        var cash = await Account(o, branch); var bank = await Account(o, branch, "Bank"); await Open(o, cash.Id);
        var advance = await Read<FinanceEntryDto>(await Finance(o, "entries", new FinanceRequest(branch, "SupplierPayment", 300, "Supplier advance", Key(), bank.Id, SupplierId: supplier.Id)));
        var allocation = await Read<FinanceEntryDto>(await Finance(o, "entries", new FinanceRequest(branch, "Allocate", 200, "Apply supplier advance", Key(), ParentId: advance.Id, PurchaseId: doc.Id)));
        doc = await Read<PurchaseDto>(await Purchase($"purchases/{doc.Id}")); Assert.Equal(0, doc.Outstanding); Assert.Equal(200, doc.Paid);
        var credit = await Read<CreditDto>(await Finance(o, $"credit?branchId={branch}&partyId={supplier.Id}&supplier=true&asOf={Today():yyyy-MM-dd}")); Assert.Equal(-100, credit.Balance); Assert.Equal(100, credit.Unapplied);
        doc = await Read<PurchaseDto>(await Purchase($"purchases/{doc.Id}/cost-payments", new PurchasePaymentRequest(Key(), 25, "Paid loader separately", doc.Entries[^1].Id)));
        Assert.Equal(975, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single(x => x.Id == cash.Id).Balance);
        Assert.Equal(HttpStatusCode.Conflict, (await Purchase($"purchases/{doc.Id}/reverse", new PurchaseReverseRequest(Key(), "Blocked by allocation"))).StatusCode);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{allocation.Id}/reverse", new FinanceReverseRequest("Release advance", Key())));
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{advance.Id}/reverse", new FinanceReverseRequest("Supplier repaid advance", Key())));
        var cost = doc.Entries.Single(x => x.Kind == "CostPayment");
        await Read<PurchaseDto>(await Purchase($"purchases/{doc.Id}/payments/{cost.Id}/reverse", new PurchaseReverseRequest(Key(), "Returned loader payment")));
        Assert.Equal(1000, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single(x => x.Id == cash.Id).Balance);
        await Read<PurchaseDto>(await Purchase($"purchases/{doc.Id}/reverse", new PurchaseReverseRequest(Key(), "Correct goods")));
    }
    [Fact]
    public async Task FinanceBranchAccessReopenPermissionAndCompositeReferencesAreEnforced()
    {
        var o = await Owner(); using var client = o.Client; var main = await BranchId(o);
        var branch = await Read<BranchDto>(await Send(client, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Restricted branch"), o.Me.TenantId));
        var account = await Account(o, branch.Id); var register = await Open(o, account.Id);
        register = await Read<RegisterDto>(await Finance(o, "register", new RegisterRequestDto(account.Id, Today(), "Close", 1000, "Counted", Key(), register.LastEventId)));
        await using var db = database.CreateContext(); var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {Permissions.All.Where(x => x != Permissions.RegisterReopen).ToArray()} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Finance(o, "register", new RegisterRequestDto(account.Id, Today(), "Reopen", 1000, "Denied", Key(), register.LastEventId))).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {Permissions.All.Where(x => x != Permissions.BranchManage).ToArray()}, \"IsOwner\" = FALSE WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        foreach (var path in new[] { $"accounts?branchId={branch.Id}", $"entries?branchId={branch.Id}", $"open-items?branchId={branch.Id}", $"register?accountId={account.Id}&date={Today():yyyy-MM-dd}" }) Assert.Equal(HttpStatusCode.NotFound, (await Finance(o, path)).StatusCode);
        var options = new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var scoped = new Gas365DbContext(options, new TenantScope { TenantId = o.Me.TenantId });
        scoped.FinanceEntries.Add(new FinanceEntry { TenantId = o.Me.TenantId, BranchId = main, AccountId = account.Id, Kind = "Opening", Amount = 0, Cash = 0, Reason = "Invalid cross-branch account", BusinessDate = Today(), IdempotencyKey = Key(), RequestHash = "test" });
        await Assert.ThrowsAsync<DbUpdateException>(() => scoped.SaveChangesAsync());
    }
    [Fact]
    public async Task FinanceForeignCustomerSupplierAllocationAndRegisterWritesAreHidden()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var (sale, ab, customer) = await CreditSale(a); var bb = await BranchId(b); var cash = await Account(a, ab); var bank = await Account(b, bb, "Bank");
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, $"credit?branchId={bb}&partyId={customer}&supplier=false&asOf={Today():yyyy-MM-dd}")).StatusCode);
        Assert.Empty(await Read<FinancePartyDto[]>(await Finance(b, "parties?supplier=false")));
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, "entries", new FinanceRequest(bb, "CustomerReceipt", 10, "Foreign", Key(), bank.Id, CustomerId: customer))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, "register", new RegisterRequestDto(cash.Id, Today(), "Open", 0, "Foreign", Key(), null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, "accounts", new MoneyAccountRequest(ab, "Foreign", "Bank", 0, "Foreign", Key()))).StatusCode);
        var bankA = await Account(a, ab, "Bank"); var advance = await Money(a, ab, bankA.Id, "CustomerReceipt", 100, customer: customer);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, "entries", new FinanceRequest(bb, "Allocate", 100, "Foreign", Key(), ParentId: advance.Id, SaleId: sale.Id))).StatusCode);
    }
}
