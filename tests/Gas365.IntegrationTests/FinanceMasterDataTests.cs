using System.Net;
using Gas365.Application;
using Microsoft.EntityFrameworkCore;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    private static Task<HttpResponseMessage> FinancePut((HttpClient Client, MeDto Me) owner, string path, object body) => Send(owner.Client, "/api/workspace/finance/" + path, HttpMethod.Put, body, owner.Me.TenantId);

    [Fact]
    public async Task CustomerAdvanceSettlesCylinderChargesAndBlocksReturnReversalUntilReversed()
    {
        var o = await Owner(); using var client = o.Client; var (draft, _, _, warehouse) = await CylinderDraft(o); var branch = warehouse.BranchId;
        var sale = await Read<SaleDto>(await Sales(o, $"drafts/{draft.Id}/post", new SalePostRequest(Key(), draft.Version, 0, [["FULL-1"]], [new([], 1000)])));
        var loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{sale.LoanIds![0]}"));
        var bank = await Account(o, branch, "Bank", 0);
        var advance = await Money(o, branch, bank.Id, "CustomerReceipt", 600, customer: sale.CustomerId);
        var early = new FinanceRequest(branch, "Allocate", 100, "Before return", Key(), ParentId: advance.Id, CylinderLoanId: loan.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", early)).StatusCode);
        loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}/return", new LoanReturnRequest(Key(), loan.Entries[^1].Id, "Empty", 1500, null, "Usage above deposit")));
        Assert.Equal(500, loan.AmountToCollect);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", early with { Amount = 501, IdempotencyKey = Key() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Finance(o, "entries", early with { IdempotencyKey = Key(), SaleId = sale.Id })).StatusCode);
        var credit = await Read<CreditDto>(await Finance(o, $"credit?branchId={branch}&partyId={sale.CustomerId}&supplier=false&asOf={Today():yyyy-MM-dd}"));
        var line = credit.Invoices.Single(x => x.Kind == "CylinderLoan"); Assert.Equal(loan.Id, line.Id); Assert.Equal(500, line.Outstanding);
        var allocated = await Read<FinanceEntryDto>(await Finance(o, "entries", early with { Amount = 300, IdempotencyKey = Key() }));
        Assert.Equal(loan.Id, allocated.CylinderLoanId);
        loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}"));
        Assert.Equal(200, loan.AmountToCollect); Assert.Equal(300, (await Read<FinanceEntryDto[]>(await Finance(o, $"open-items?branchId={branch}"))).Single().Unapplied);
        Assert.Equal(1000, loan.Entries.Sum(x => x.Cash)); // The allocation moves no cash; only the original deposit was collected.
        var returnEntry = loan.Entries.Single(x => x.Kind == "Return");
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(o, $"loans/{loan.Id}/entries/{returnEntry.Id}/reverse", new SaleReverseRequest(Key(), "Blocked by allocation"))).StatusCode);
        await Read<FinanceEntryDto>(await Finance(o, $"entries/{allocated.Id}/reverse", new FinanceReverseRequest("Allocation correction", Key())));
        loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}"));
        Assert.Equal(500, loan.AmountToCollect);
        Assert.Equal(loan.Entries.Single(x => x.Kind == "Allocation").Id, loan.Entries.Single(x => x.Kind == "AllocationReversal").ReversesId);
        loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}/entries/{returnEntry.Id}/reverse", new SaleReverseRequest(Key(), "Return entered in error")));
        Assert.Equal("On loan", loan.Status);
        var foreign = await Owner(); using var fc = foreign.Client; var fb = await BranchId(foreign); var fbank = await Account(foreign, fb, "Bank", 0);
        var foreignAdvance = await Money(foreign, fb, fbank.Id, "CustomerReceipt", 10, customer: (await Read<CustomerDto>(await Sales(foreign, "customers", new CustomerRequest("Foreign", "", "")))).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await Finance(foreign, "entries", new FinanceRequest(fb, "Allocate", 5, "Foreign loan", Key(), ParentId: foreignAdvance.Id, CylinderLoanId: loan.Id))).StatusCode);
    }

    [Fact]
    public async Task AccountsAndCategoriesRenameAndRetireWithoutRewritingHistory()
    {
        var o = await Owner(); using var client = o.Client; var branch = await BranchId(o);
        var drawer = await Account(o, branch, opening: 0); var bank = await Account(o, branch, "Bank", 100);
        var renamed = await Read<MoneyAccountDto>(await FinancePut(o, $"accounts/{bank.Id}", new MasterUpdateRequest("Meezan current", true, bank.Version)));
        Assert.Equal("Meezan current", renamed.Name); Assert.NotEqual(bank.Version, renamed.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await FinancePut(o, $"accounts/{bank.Id}", new MasterUpdateRequest("Stale", true, bank.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FinancePut(o, $"accounts/{drawer.Id}", new MasterUpdateRequest(drawer.Name, false, drawer.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FinancePut(o, $"accounts/{bank.Id}", new MasterUpdateRequest(renamed.Name, false, renamed.Version))).StatusCode);
        await Money(o, branch, bank.Id, "OwnerWithdrawal", 100);
        var retired = await Read<MoneyAccountDto>(await FinancePut(o, $"accounts/{bank.Id}", new MasterUpdateRequest(renamed.Name, false, renamed.Version)));
        Assert.False(retired.Active);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", new FinanceRequest(branch, "OwnerFunds", 5, "Retired", Key(), bank.Id))).StatusCode);
        var entries = await Read<PageDto<FinanceEntryDto>>(await Finance(o, $"entries?branchId={branch}"));
        Assert.Contains(entries.Items, x => x.AccountId == bank.Id && x.Kind == "OwnerWithdrawal");
        var active = await Read<MoneyAccountDto>(await FinancePut(o, $"accounts/{bank.Id}", new MasterUpdateRequest(renamed.Name, true, retired.Version)));
        await Money(o, branch, bank.Id, "OwnerFunds", 5); Assert.True(active.Active);

        var category = await Read<ExpenseCategoryDto>(await Finance(o, "categories", new ExpenseCategoryRequest("Fuel", false)));
        var retiredCategory = await Read<ExpenseCategoryDto>(await FinancePut(o, $"categories/{category.Id}", new MasterUpdateRequest("Vehicle fuel", false, category.Version)));
        Assert.Equal("Vehicle fuel", retiredCategory.Name); Assert.False(retiredCategory.Active); Assert.False(retiredCategory.Capital);
        Assert.Equal(HttpStatusCode.Conflict, (await Finance(o, "entries", new FinanceRequest(branch, "Expense", 1, "Retired category", Key(), bank.Id, CategoryId: category.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FinancePut(o, $"categories/{category.Id}", new MasterUpdateRequest("Stale", true, category.Version))).StatusCode);

        var other = await Owner(); using var oc = other.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await FinancePut(other, $"accounts/{bank.Id}", new MasterUpdateRequest("Foreign", true, active.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FinancePut(other, $"categories/{category.Id}", new MasterUpdateRequest("Foreign", true, retiredCategory.Version))).StatusCode);

        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"MoneyAccounts\" WHERE \"Id\" = {bank.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MoneyAccounts\" SET \"Kind\" = 'Cash' WHERE \"Id\" = {bank.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MoneyAccounts\" SET \"Active\" = FALSE WHERE \"Id\" = {drawer.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ExpenseCategories\" SET \"Capital\" = TRUE WHERE \"Id\" = {category.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ExpenseCategories\" WHERE \"Id\" = {category.Id}"));

        var role = await db.TenantRoles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == o.Me.TenantId && x.IsOwner);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TenantRoles\" SET \"Permissions\" = {new[] { Gas365.Domain.Permissions.FinanceView, Gas365.Domain.Permissions.FinancePost }} WHERE \"TenantId\" = {o.Me.TenantId} AND \"Id\" = {role.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await FinancePut(o, $"accounts/{bank.Id}", new MasterUpdateRequest("Denied", true, active.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await FinancePut(o, $"categories/{category.Id}", new MasterUpdateRequest("Denied", true, retiredCategory.Version))).StatusCode);
    }
}
