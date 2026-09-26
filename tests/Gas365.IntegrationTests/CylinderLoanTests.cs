using System.Net;
using System.Net.Http.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    [Theory]
    [InlineData("Cylinder", "CylinderOnly", "Empty")]
    [InlineData("SparePart", "SparePart", "Available")]
    public async Task ShellOnlyAndSparePartSalesUseTheirOwnCostWithoutConsumingGas(string kind, string mode, string bucket)
    {
        var owner = await Owner(); using var client = owner.Client;
        var (item, warehouse) = await Setup(owner, "Cylinder");
        if (kind == "SparePart")
        {
            var category = await Read<CategoryDto>(await Stock(owner, "categories", new CategoryRequest("Accessories")));
            item = await Read<ItemDto>(await Stock(owner, "items", new ItemRequest("VALVE", "Valve", kind, item.UnitId, category.Id, "", "", null, null, false, 0)));
        }
        await Read<DocumentDto>(await Stock(owner, "documents", new PostStockRequest(Guid.NewGuid().ToString(), "Opening", item.Id, warehouse.Id, bucket, 2, 1000, null, null, "Opening", 0, 0, [])));
        var customer = await Read<CustomerDto>(await Sales(owner, "customers", new CustomerRequest("Customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(owner, "price-lists", new PriceListRequest("Retail")));
        var price = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{list.Id}/prices", new SalePriceRequest(item.Id, mode, 1500)));
        var draft = await Read<SaleDraftDto>(await Sales(owner, "drafts", new SaleDraftRequest(warehouse.BranchId, customer.Id, list.Id, "Shell or part", "", [new(price.Id, price.Version, warehouse.Id, 1, 0)])));
        var sale = await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 1500, [[]])));
        Assert.Equal(1000, sale.Cogs); Assert.Equal(500, sale.GrossProfit); Assert.Empty(sale.LoanIds!);
        Assert.Equal(1, (await Read<StockBalanceDto[]>(await Stock(owner, $"items/{item.Id}/balances"))).Single().Quantity);
    }
    [Fact]
    public async Task ConcurrentCylinderReturnsHaveOneWinnerAndDoNotDuplicateRefundLiability()
    {
        var owner = await Owner(); using var client = owner.Client;
        var (draft, _, _, _) = await CylinderDraft(owner);
        var sale = await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 0, [["FULL-1"]], [new([], 5000)])));
        var loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{sale.LoanIds![0]}"));
        var r = new LoanReturnRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Empty", 2000, null, "Return");
        var results = await Task.WhenAll(Sales(owner, $"loans/{loan.Id}/return", r), Sales(owner, $"loans/{loan.Id}/return", r with { IdempotencyKey = Guid.NewGuid().ToString() }));
        Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{loan.Id}"));
        Assert.Equal(3000, loan.RefundDue); Assert.Single(loan.Entries, x => x.Kind == "Return");
    }
    private async Task<(SaleDraftDto Draft, ItemDto Shell, ItemDto Gas, WarehouseDto Warehouse)> CylinderDraft((HttpClient Client, MeDto Me) owner, string mode = "LoanRefill", decimal count = 1)
    {
        var (shell, warehouse) = await Setup(owner, "Cylinder", true);
        var emptyPolicy = await Sales(owner, $"cylinder-policies/{shell.Id}");
        emptyPolicy.EnsureSuccessStatusCode();
        Assert.Equal("null", await emptyPolicy.Content.ReadAsStringAsync());
        foreach (var serial in new[] { "FULL-1", "FULL-2", "EMPTY-IN" }) await Read<AssetDto>(await Stock(owner, "assets", new AssetRequest(shell.Id, serial, "Business", "", null)));
        await Read<DocumentDto>(await Stock(owner, "documents", new PostStockRequest(Guid.NewGuid().ToString(), "Opening", shell.Id, warehouse.Id, "Full", 2, 1000, null, null, "Shells only", 0, 0, ["FULL-1", "FULL-2"])));
        var kg = await Read<UnitDto>(await Stock(owner, "units", new UnitRequest("KG", 3)));
        var gas = await Read<ItemDto>(await Stock(owner, "items", new ItemRequest("BULK-LPG", "LPG", "Lpg", kg.Id, null, "", "Domestic", 45, null, false, 0)));
        await Read<DocumentDto>(await Stock(owner, "documents", new PostStockRequest(Guid.NewGuid().ToString(), "Opening", gas.Id, warehouse.Id, "Available", 200, 100, null, null, "Gas separately", 0, 0, [])));
        await Read<CylinderPolicyDto>(await Sales(owner, $"cylinder-policies/{shell.Id}", new CylinderPolicyRequest(gas.Id, 45, 5000, "Usage fee is entered on return; approved damage charges may apply."), HttpMethod.Put));
        var customer = await Read<CustomerDto>(await Sales(owner, "customers", new CustomerRequest("Cylinder customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(owner, "price-lists", new PriceListRequest("Cylinder retail")));
        var price = await Read<SalePriceDto>(await Sales(owner, $"price-lists/{list.Id}/prices", new SalePriceRequest(shell.Id, mode, 9000)));
        var draft = await Read<SaleDraftDto>(await Sales(owner, "drafts", new SaleDraftRequest(warehouse.BranchId, customer.Id, list.Id, "Cylinder sale", "", [new(price.Id, price.Version, warehouse.Id, count, 0)])));
        return (draft, shell, gas, warehouse);
    }
    private async Task<(HttpClient Client, MeDto Me)> CylinderWorker((HttpClient Client, MeDto Me) owner, Guid branch, string[] permissions)
    {
        var role = await Read<RoleDto>(await Send(owner.Client, "/api/workspace/roles", HttpMethod.Post, new RoleRequest("Cylinder worker", permissions), owner.Me.TenantId));
        var email = $"cylinder-worker-{Guid.NewGuid():N}@test.example";
        (await Send(owner.Client, "/api/workspace/members", HttpMethod.Post, new InviteRequest(email, "Cylinder worker", role.Id, [branch]), owner.Me.TenantId)).EnsureSuccessStatusCode();
        var worker = Client();
        (await Send(worker, "/api/auth/reset", HttpMethod.Post, new ResetRequest(email, messages.Token(email), Password))).EnsureSuccessStatusCode();
        (await Send(worker, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        return (worker, (await worker.GetFromJsonAsync<MeDto>("/api/auth/me"))!);
    }
    [Fact]
    public async Task BorrowingAndWorkerReturnKeepDepositsSeparateAndRefundOnlyActualCash()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, shell, gas, warehouse) = await CylinderDraft(owner);
        var post = new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 9000, [["FULL-1"]], [new([], 5000)]);
        var sale = await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", post));
        Assert.Equal(4500, sale.Cogs); Assert.Equal(9000, sale.Entries.Sum(x => x.Revenue)); Assert.Equal(9000, sale.Paid); Assert.Equal(5000, sale.DepositCollected);
        var loanId = Assert.Single(sale.LoanIds!);
        var loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{loanId}"));
        Assert.Equal(5000, loan.Entries.Sum(x => x.Liability)); Assert.Equal(0, loan.Entries.Sum(x => x.Revenue));
        Assert.Equal(155, (await Read<StockBalanceDto[]>(await Stock(owner, $"items/{gas.Id}/balances"))).Single().Quantity);
        var shells = await Read<StockBalanceDto[]>(await Stock(owner, $"items/{shell.Id}/balances")); Assert.Equal(1, shells.Single(x => x.Bucket == "CustomerHeld").Quantity); Assert.All(shells, x => Assert.Equal(2000, x.WarehouseValue));
        var worker = await CylinderWorker(owner, warehouse.BranchId, [Permissions.SalesView, Permissions.CylinderReturn, Permissions.SalesPay]); using var workerClient = worker.Client;
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(worker, $"loans/{loanId}/approvals", new LoanApprovalRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Damaged", 1000, "Not allowed"))).StatusCode);
        var ret = new LoanReturnRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Empty", 1275.50m, null, "Customer returned cylinder");
        loan = await Read<CylinderLoanDto>(await Sales(worker, $"loans/{loanId}/return", ret));
        Assert.Equal("Returned", loan.Status); Assert.Equal(3724.50m, loan.RefundDue); Assert.Equal(1275.50m, loan.Entries.Sum(x => x.Revenue)); Assert.Equal(5000, loan.Entries.Sum(x => x.Cash));
        Assert.Equal(2, (await Read<CylinderLoanDto>(await Sales(worker, $"loans/{loanId}/return", ret))).Entries.Length);
        var refund = new LoanPaymentRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, 3724.50m, "Cash handed to customer");
        loan = await Read<CylinderLoanDto>(await Sales(worker, $"loans/{loanId}/refunds", refund)); Assert.Equal(0, loan.RefundDue); Assert.Equal(1275.50m, loan.Entries.Sum(x => x.Cash));
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(owner, $"loans/{loanId}/entries/{loan.Entries.Single(x => x.Kind == "Return").Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Correction"))).StatusCode);
        var refunded = loan.Entries.Single(x => x.Kind == "Refund");
        loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{loanId}/entries/{refunded.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Refund entered in error")));
        var returned = loan.Entries.Single(x => x.Kind == "Return");
        loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{loanId}/entries/{returned.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Return entered in error")));
        Assert.Equal("On loan", loan.Status); Assert.Equal(5000, loan.Entries.Sum(x => x.Liability)); Assert.Equal(0, loan.Entries.Sum(x => x.Revenue));
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"CylinderLoanEntries\" WHERE \"LoanId\" = {loanId}"));
    }
    [Fact]
    public async Task ZeroDepositArrangementAndManagerApprovedLossProduceReceivableWithoutStockReturn()
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, shell, _, warehouse) = await CylinderDraft(owner);
        var worker = await CylinderWorker(owner, warehouse.BranchId, [Permissions.SalesView, Permissions.SalesPost, Permissions.CylinderReturn, Permissions.SalesPay]); using var workerClient = worker.Client;
        var post = new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 0, [["FULL-1"]], [new([], 0)]);
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(worker, $"drafts/{draft.Id}/post", post)).StatusCode);
        Assert.Empty((await Read<PageDto<CylinderLoanDto>>(await Sales(owner, $"loans?branchId={warehouse.BranchId}"))).Items);
        await Read<DepositArrangementDto>(await Sales(owner, $"customers/{draft.CustomerId}/deposit-arrangement", new DepositArrangementRequest(true, "Hotel zero-deposit arrangement"), HttpMethod.Put));
        var sale = await Read<SaleDto>(await Sales(worker, $"drafts/{draft.Id}/post", post)); Assert.Null(sale.Cogs); Assert.Null(sale.GrossProfit);
        var loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{sale.LoanIds![0]}"));
        var ret = new LoanReturnRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Lost", 2000, null, "Cylinder not returned");
        Assert.Equal(HttpStatusCode.Forbidden, (await Sales(worker, $"loans/{loan.Id}/return", ret)).StatusCode);
        loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{loan.Id}/approvals", new LoanApprovalRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Lost", 1000, "Manager approved loss charge")));
        ret = ret with { ExpectedLastEntryId = loan.Entries[^1].Id, ApprovalId = loan.Entries[^1].Id };
        loan = await Read<CylinderLoanDto>(await Sales(worker, $"loans/{loan.Id}/return", ret));
        Assert.Equal("Lost", loan.Status); Assert.Equal(0, loan.RefundDue); Assert.Equal(3000, loan.AmountToCollect);
        var balances = await Read<StockBalanceDto[]>(await Stock(owner, $"items/{shell.Id}/balances")); Assert.Equal(1, balances.Single(x => x.Bucket == "Lost").Quantity); Assert.DoesNotContain(balances, x => x.Bucket == "Empty" && x.Quantity > 0);
        loan = await Read<CylinderLoanDto>(await Sales(worker, $"loans/{loan.Id}/payments", new LoanPaymentRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, 3000, "Charges collected")));
        Assert.Equal(0, loan.AmountToCollect); Assert.Equal(3000, loan.Entries.Sum(x => x.Cash));
    }
    [Theory]
    [InlineData("Exchange", 4500)]
    [InlineData("FullCylinder", 5500)]
    public async Task FullAndExchangeDeductGasExactlyOnceAndReverseExactly(string mode, decimal cogs)
    {
        var owner = await Owner(); using var client = owner.Client; var (draft, shell, gas, _) = await CylinderDraft(owner, mode);
        var post = new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 0, [["FULL-1"]], [new(mode == "Exchange" ? ["EMPTY-IN"] : [], 0)]);
        var sale = await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", post)); Assert.Equal(cogs, sale.Cogs);
        Assert.Equal(155, (await Read<StockBalanceDto[]>(await Stock(owner, $"items/{gas.Id}/balances"))).Single().Quantity);
        var shells = await Read<StockBalanceDto[]>(await Stock(owner, $"items/{shell.Id}/balances")); Assert.Equal(mode == "Exchange" ? 2 : 1, shells.Sum(x => x.Quantity));
        if (mode == "Exchange") { Assert.Equal(1, shells.Single(x => x.Bucket == "Empty").Quantity); Assert.All(shells, x => Assert.Equal(2000, x.WarehouseValue)); }
        await Read<SaleDto>(await Sales(owner, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Reversing exchange or full-cylinder sale")));
        Assert.Equal(200, (await Read<StockBalanceDto[]>(await Stock(owner, $"items/{gas.Id}/balances"))).Single().Quantity);
        Assert.Equal(2, (await Read<StockBalanceDto[]>(await Stock(owner, $"items/{shell.Id}/balances"))).Single(x => x.Bucket == "Full").Quantity);
    }
    [Fact]
    public async Task CylinderRoutesRejectForeignReferencesAndLoanIssueReversalRefundsDeposit()
    {
        var owner = await Owner(); var foreign = await Owner(); using var c = owner.Client; using var f = foreign.Client;
        var (draft, shell, gas, warehouse) = await CylinderDraft(owner);
        var sale = await Read<SaleDto>(await Sales(owner, $"drafts/{draft.Id}/post", new SalePostRequest(Guid.NewGuid().ToString(), draft.Version, 0, [["FULL-1"]], [new([], 5000)])));
        var loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{sale.LoanIds![0]}"));
        var routes = new (string Path, HttpMethod Method, object? Body)[] {
            ($"cylinder-policies/{shell.Id}", HttpMethod.Get, null), ($"cylinder-policies/{shell.Id}", HttpMethod.Put, new CylinderPolicyRequest(gas.Id, 45, 0, "Terms")),
            ($"customers/{draft.CustomerId}/deposit-arrangement", HttpMethod.Get, null), ($"customers/{draft.CustomerId}/deposit-arrangement", HttpMethod.Put, new DepositArrangementRequest(true, "Reason")),
            ($"loans?branchId={warehouse.BranchId}", HttpMethod.Get, null), ($"loans/{loan.Id}", HttpMethod.Get, null),
            ($"loans/{loan.Id}/return", HttpMethod.Post, new LoanReturnRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Empty", 0, null, "Return")),
            ($"loans/{loan.Id}/approvals", HttpMethod.Post, new LoanApprovalRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, "Lost", 0, "Loss")),
            ($"loans/{loan.Id}/payments", HttpMethod.Post, new LoanPaymentRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, 1, "Pay")),
            ($"loans/{loan.Id}/refunds", HttpMethod.Post, new LoanPaymentRequest(Guid.NewGuid().ToString(), loan.Entries[^1].Id, 1, "Refund")),
            ($"loans/{loan.Id}/entries/{loan.Entries[0].Id}/reverse", HttpMethod.Post, new SaleReverseRequest(Guid.NewGuid().ToString(), "Reverse")) };
        foreach (var route in routes) Assert.Equal(HttpStatusCode.NotFound, (await Sales(foreign, route.Path, route.Body, route.Method)).StatusCode);
        var worker = await CylinderWorker(owner, warehouse.BranchId, []); using var workerClient = worker.Client;
        foreach (var route in routes) Assert.Equal(HttpStatusCode.Forbidden, (await Sales(worker, route.Path, route.Body, route.Method)).StatusCode);
        await Read<SaleDto>(await Sales(owner, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Guid.NewGuid().ToString(), "Cylinder returned and full deposit cash handed back")));
        loan = await Read<CylinderLoanDto>(await Sales(owner, $"loans/{loan.Id}")); Assert.Equal("Reversed", loan.Status); Assert.Equal(0, loan.Entries.Sum(x => x.Cash)); Assert.Equal(0, loan.Entries.Sum(x => x.Liability));
    }
}
