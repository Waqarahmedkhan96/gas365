using System.Net;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    private static Task<HttpResponseMessage> Finance((HttpClient Client, MeDto Me) owner, string path, object? body = null) => Send(owner.Client, "/api/workspace/finance/" + path, body is null ? HttpMethod.Get : HttpMethod.Post, body, owner.Me.TenantId);
    private static string Key() => Guid.NewGuid().ToString();
    private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi")).DateTime);
    private static async Task<Guid> BranchId((HttpClient Client, MeDto Me) owner) => (await Read<BranchDto[]>(await Send(owner.Client, "/api/workspace/branches", HttpMethod.Get, tenant: owner.Me.TenantId)))[0].Id;
    private static async Task<MoneyAccountDto> Account((HttpClient Client, MeDto Me) owner, Guid branch, string kind = "Cash", decimal opening = 1000) => await Read<MoneyAccountDto>(await Finance(owner, "accounts", new MoneyAccountRequest(branch, kind + Key(), kind, opening, "Reconciled opening", Key())));
    private static async Task<RegisterDto> Open((HttpClient Client, MeDto Me) owner, Guid account) => await Read<RegisterDto>(await Finance(owner, "register", new RegisterRequestDto(account, Today(), "Open", 1000, "Opening count", Key(), null)));
    private static async Task<FinanceEntryDto> Money((HttpClient Client, MeDto Me) owner, Guid branch, Guid account, string kind, decimal amount, Guid? parent = null, Guid? customer = null, Guid? category = null) => await Read<FinanceEntryDto>(await Finance(owner, "entries", new FinanceRequest(branch, kind, amount, "Test " + kind, Key(), account, customer, CategoryId: category, ParentId: parent, Counterparty: "Lender")));

    [Fact]
    public async Task FinanceRegisterReconciliationClosedDayReopeningAndIdempotency()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o); var account = await Account(o, branch); var bank = await Account(o, branch, "Bank", 0);
        var command = new FinanceRequest(branch, "OwnerFunds", 200, "Owner investment", Key(), account.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", command)).StatusCode);
        var register = await Open(o, account.Id); Assert.Equal(1000, register.Opening);
        var e = await Read<FinanceEntryDto>(await Finance(o, "entries", command));
        Assert.Equal(e.Id, (await Read<FinanceEntryDto>(await Finance(o, "entries", command))).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", command with { Amount = 201 })).StatusCode);
        var category = await Read<ExpenseCategoryDto>(await Finance(o, "categories", new ExpenseCategoryRequest("Utilities", false)));
        await Money(o, branch, account.Id, "Expense", 100, category: category.Id);
        var transfer = await Read<FinanceEntryDto>(await Finance(o, "entries", new FinanceRequest(branch, "Transfer", 150, "Bank deposit", Key(), account.Id, DestinationAccountId: bank.Id)));
        register = await Read<RegisterDto>(await Finance(o, $"register?accountId={account.Id}&date={Today():yyyy-MM-dd}")); Assert.Equal(950, register.Expected);
        var close = new RegisterRequestDto(account.Id, Today(), "Close", 940, "Ten rupees short", Key(), register.LastEventId);
        register = await Read<RegisterDto>(await Finance(o, "register", close)); Assert.Equal("Closed", register.Status); Assert.Equal(-10, register.History[^1].Variance);
        Assert.Equal(register.LastEventId, (await Read<RegisterDto>(await Finance(o, "register", close))).LastEventId);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", command with { IdempotencyKey = Key() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, $"entries/{transfer.Id}/reverse", new FinanceReverseRequest("Return deposit", Key()))).StatusCode);
        var reopen = new RegisterRequestDto(account.Id, Today(), "Reopen", 940, "Investigate shortage", Key(), register.LastEventId);
        register = await Read<RegisterDto>(await Finance(o, "register", reopen));
        await Money(o, branch, account.Id, "CashAdjustmentOut", 10);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{transfer.Id}/reverse", new FinanceReverseRequest("Bank transfer correction", Key())));
        var accounts = await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}")); Assert.Equal(1090, accounts.Single(x => x.Id == account.Id).Balance); Assert.Equal(0, accounts.Single(x => x.Id == bank.Id).Balance);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "register", close with { IdempotencyKey = Key() })).StatusCode);
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"FinanceEntries\" SET \"Cash\" = 1 WHERE \"Id\" = {e.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"RegisterEvents\" WHERE \"AccountId\" = {account.Id}"));
    }
    [Fact]
    public async Task FinanceLoansRepaymentsAndReversalDependencies()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o); var bank = await Account(o, branch, "Bank");
        var loan = await Money(o, branch, bank.Id, "LoanReceived", 500);
        var repayment = await Money(o, branch, bank.Id, "LoanRepayment", 200, loan.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", new FinanceRequest(branch, "LoanRepayment", 301, "Too much", Key(), bank.Id, ParentId: loan.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, $"entries/{loan.Id}/reverse", new FinanceReverseRequest("Correction", Key()))).StatusCode);
        var remaining = await Read<FinanceEntryDto[]>(await Finance(o, $"open-items?branchId={branch}")); Assert.Equal(300, remaining.Single().Unapplied);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{repayment.Id}/reverse", new FinanceReverseRequest("Repayment returned", Key())));
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{loan.Id}/reverse", new FinanceReverseRequest("Loan returned", Key())));
        Assert.Empty(await Read<FinanceEntryDto[]>(await Finance(o, $"open-items?branchId={branch}")));
        Assert.Equal(1000, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single().Balance);
    }
    private static async Task<(SaleDto Sale, Guid Branch, Guid Customer)> CreditSale((HttpClient Client, MeDto Me) o)
    {
        var (item, warehouse) = await Setup(o);
        await Read<DocumentDto>(await Stock(o, "documents", new PostStockRequest(Key(), "Opening", item.Id, warehouse.Id, "Available", 10, 100, null, null, "Opening", 0, 0, [])));
        var customer = await Read<CustomerDto>(await Sales(o, "customers", new CustomerRequest("Credit customer", "", "")));
        var list = await Read<PriceListDto>(await Sales(o, "price-lists", new PriceListRequest("Credit prices")));
        var price = await Read<SalePriceDto>(await Sales(o, $"price-lists/{list.Id}/prices", new SalePriceRequest(item.Id, "Refill", 200)));
        var draft = await Read<SaleDraftDto>(await Sales(o, "drafts", new SaleDraftRequest(warehouse.BranchId, customer.Id, list.Id, "Credit invoice", "", [new(price.Id, price.Version, warehouse.Id, 2, 0)])));
        var sale = await Read<SaleDto>(await Sales(o, $"drafts/{draft.Id}/post", new SalePostRequest(Key(), draft.Version, 0, [[]])));
        return (sale, warehouse.BranchId, customer.Id);
    }
    [Fact]
    public async Task CustomerAdvancesAllocateWithoutDuplicateCashAndReverseInOrder()
    {
        var o = await Owner(); using var client = o.Client; var (sale, branch, customer) = await CreditSale(o); var bank = await Account(o, branch, "Bank", 0);
        var payment = await Money(o, branch, bank.Id, "CustomerReceipt", 500, customer: customer);
        var allocation = new FinanceRequest(branch, "Allocate", 400, "Allocate advance", Key(), ParentId: payment.Id, SaleId: sale.Id);
        var allocated = await Read<FinanceEntryDto>(await Finance(o, "entries", allocation));
        Assert.Equal(allocated.Id, (await Read<FinanceEntryDto>(await Finance(o, "entries", allocation))).Id);
        var detail = await Read<SaleDto>(await Sales(o, $"posted/{sale.Id}")); Assert.Equal(0, detail.Outstanding); Assert.Equal(400, detail.Paid);
        var credit = await Read<CreditDto>(await Finance(o, $"credit?branchId={branch}&partyId={customer}&supplier=false&asOf={Today():yyyy-MM-dd}"));
        Assert.Equal(-100, credit.Balance); Assert.Equal(100, credit.Unapplied); Assert.Equal(0, credit.Invoices.Single().Outstanding);
        Assert.Equal(500, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single().Balance);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", allocation with { IdempotencyKey = Key(), Amount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, $"entries/{payment.Id}/reverse", new FinanceReverseRequest("Undo advance", Key()))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(o, $"posted/{sale.Id}/reverse", new SaleReverseRequest(Key(), "Undo invoice"))).StatusCode);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{allocated.Id}/reverse", new FinanceReverseRequest("Release allocation", Key())));
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{payment.Id}/reverse", new FinanceReverseRequest("Return advance", Key())));
        detail = await Read<SaleDto>(await Sales(o, $"posted/{sale.Id}")); Assert.Equal(400, detail.Outstanding); Assert.Equal(0, detail.Paid);
    }
    [Fact]
    public async Task ExistingSaleCashIsCapturedAndClosedRegisterCannotBeBypassed()
    {
        var o = await Owner(); using var client = o.Client; var (sale, branch, _) = await CreditSale(o); var cash = await Account(o, branch, opening: 0); var register = await Open(o, cash.Id);
        sale = await Read<SaleDto>(await Sales(o, $"posted/{sale.Id}/payments", new SalePaymentRequest(Key(), 100, "Actual cash", sale.Entries[^1].Id)));
        Assert.Equal(100, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single().Balance);
        register = await Read<RegisterDto>(await Finance(o, "register", new RegisterRequestDto(cash.Id, Today(), "Close", 100, "Count checked", Key(), register.LastEventId)));
        var blocked = await Sales(o, $"posted/{sale.Id}/payments", new SalePaymentRequest(Key(), 10, "Cannot bypass", sale.Entries[^1].Id));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode); Assert.Contains("reopening", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(300, (await Read<SaleDto>(await Sales(o, $"posted/{sale.Id}"))).Outstanding);
        await Read<RegisterDto>(await Finance(o, "register", new RegisterRequestDto(cash.Id, Today(), "Reopen", 100, "Correct receipt", Key(), register.LastEventId)));
        var payment = sale.Entries.Single(x => x.Kind == "Payment");
        await Read<SaleDto>(await Sales(o, $"posted/{sale.Id}/payments/{payment.Id}/reverse", new SaleReverseRequest(Key(), "Cash returned")));
        Assert.Equal(0, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={branch}"))).Single().Balance);
    }
    [Fact]
    public async Task FinanceForeignReferencesReadIsolationAndLivePermissions()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var ab = await BranchId(a); var bb = await BranchId(b); var account = await Account(a, ab, "Bank"); var other = await Account(b, bb, "Bank");
        var category = await Read<ExpenseCategoryDto>(await Finance(a, "categories", new ExpenseCategoryRequest("Private category", false)));
        Assert.Empty(await Read<ExpenseCategoryDto[]>(await Finance(b, "categories")));
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, $"accounts?branchId={ab}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, $"entries?branchId={ab}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, $"open-items?branchId={ab}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, $"register?accountId={account.Id}&date={Today():yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, "entries", new FinanceRequest(bb, "Expense", 1, "Foreign", Key(), other.Id, CategoryId: category.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, "entries", new FinanceRequest(bb, "OwnerFunds", 1, "Foreign", Key(), account.Id))).StatusCode);
        var e = await Money(a, ab, account.Id, "OwnerFunds", 10);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(b, $"entries/{e.Id}/reverse", new FinanceReverseRequest("Foreign", Key()))).StatusCode);
        await using var db = database.CreateContext();
        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == a.Me.TenantId && x.IsOwner);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {new[] { Permissions.FinanceView }} WHERE \"TenantId\" = {a.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await Finance(a, "entries", new FinanceRequest(ab, "OwnerFunds", 1, "Denied", Key(), account.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Finance(a, "categories", new ExpenseCategoryRequest("Denied", false))).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {Array.Empty<string>()} WHERE \"TenantId\" = {a.Me.TenantId} AND \"Id\" = {role.Id}");
        foreach (var path in new[] { $"accounts?branchId={ab}", $"entries?branchId={ab}", "categories", "parties?supplier=false", $"open-items?branchId={ab}", $"register?accountId={account.Id}&date={Today():yyyy-MM-dd}" }) Assert.Equal(HttpStatusCode.Forbidden, (await Finance(a, path)).StatusCode);
    }
    [Fact]
    public async Task ConcurrentFinanceRepaymentsCannotOverpay()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o); var bank = await Account(o, branch, "Bank"); var loan = await Money(o, branch, bank.Id, "LoanGiven", 100);
        var first = new FinanceRequest(branch, "LoanRepayment", 100, "Payment", Key(), bank.Id, ParentId: loan.Id);
        var results = await Task.WhenAll(Finance(o, "entries", first), Finance(o, "entries", first with { IdempotencyKey = Key() }));
        Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Empty(await Read<FinanceEntryDto[]>(await Finance(o, $"open-items?branchId={branch}")));
    }
}
