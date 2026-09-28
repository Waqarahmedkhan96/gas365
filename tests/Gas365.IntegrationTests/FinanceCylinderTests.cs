using System.Net;
using Gas365.Application;

namespace Gas365.IntegrationTests;

public sealed partial class CustomerTests
{
    [Fact]
    public async Task CylinderDepositsAndRefundsEnterDrawerOnceAndRespectClosing()
    {
        var o = await Owner(); using var client = o.Client; var (draft, _, _, warehouse) = await CylinderDraft(o);
        var cash = await Account(o, warehouse.BranchId, opening: 0); var register = await Open(o, cash.Id);
        var sale = await Read<SaleDto>(await Sales(o, $"drafts/{draft.Id}/post", new SalePostRequest(Key(), draft.Version, 0, [["FULL-1"]], [new([], 5000)])));
        Assert.Equal(5000, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={warehouse.BranchId}"))).Single().Balance);
        var loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{sale.LoanIds![0]}"));
        loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}/return", new LoanReturnRequest(Key(), loan.Entries[^1].Id, "Empty", 1200, null, "Manual usage fee")));
        register = await Read<RegisterDto>(await Finance(o, "register", new RegisterRequestDto(cash.Id, Today(), "Close", 5000, "Deposit counted separately from revenue", Key(), register.LastEventId)));
        var refund = new LoanPaymentRequest(Key(), loan.Entries[^1].Id, 3800, "Actual refund");
        Assert.Equal(HttpStatusCode.Conflict, (await Sales(o, $"loans/{loan.Id}/refunds", refund)).StatusCode);
        await Read<RegisterDto>(await Finance(o, "register", new RegisterRequestDto(cash.Id, Today(), "Reopen", 5000, "Authorize refund", Key(), register.LastEventId)));
        loan = await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}/refunds", refund));
        await Read<CylinderLoanDto>(await Sales(o, $"loans/{loan.Id}/refunds", refund));
        Assert.Equal(0, loan.RefundDue);
        Assert.Equal(1200, (await Read<MoneyAccountDto[]>(await Finance(o, $"accounts?branchId={warehouse.BranchId}"))).Single().Balance);
    }
}
