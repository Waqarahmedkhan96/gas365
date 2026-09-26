namespace Gas365.Application;

public sealed record CylinderPolicyRequest(Guid GasItemId, decimal GasQuantity, decimal SuggestedDeposit, string UsageTerms, uint Version = 0);
public sealed record CylinderPolicyDto(Guid ItemId, Guid GasItemId, decimal GasQuantity, decimal SuggestedDeposit, string UsageTerms, uint Version);
public sealed record DepositArrangementRequest(bool ZeroDepositApproved, string Reason, uint Version = 0);
public sealed record DepositArrangementDto(Guid CustomerId, bool ZeroDepositApproved, string Reason, uint Version);
public sealed record LoanReturnRequest(string IdempotencyKey, Guid ExpectedLastEntryId, string Condition, decimal UsageFee, Guid? ApprovalId, string Reason);
public sealed record LoanApprovalRequest(string IdempotencyKey, Guid ExpectedLastEntryId, string Condition, decimal Charge, string Reason);
public sealed record LoanPaymentRequest(string IdempotencyKey, Guid ExpectedLastEntryId, decimal Amount, string Reason);
public sealed record LoanEntryDto(Guid Id, string Kind, decimal Cash, decimal Liability, decimal Receivable, decimal Revenue, decimal UsageFee, decimal DamageOrLossCharge,
    string Condition, string Reason, Guid ActorId, DateTimeOffset PostedAt, DateOnly BusinessDate, Guid? ReversesId, Guid? ApprovalId);
public sealed record CylinderLoanDto(Guid Id, Guid SaleId, Guid BranchId, Guid CustomerId, Guid ItemId, Guid WarehouseId, string Serial,
    decimal Deposit, string UsageTerms, string Status, decimal RefundDue, decimal AmountToCollect, LoanEntryDto[] Entries);
public interface ICylinderLoanAccess
{
    Task<CylinderPolicyDto?> PolicyAsync(Guid itemId, CancellationToken ct);
    Task<CylinderPolicyDto> SavePolicyAsync(Guid itemId, CylinderPolicyRequest request, CancellationToken ct);
    Task<DepositArrangementDto> ArrangementAsync(Guid customerId, CancellationToken ct);
    Task<DepositArrangementDto> SaveArrangementAsync(Guid customerId, DepositArrangementRequest request, CancellationToken ct);
    Task<PageDto<CylinderLoanDto>> LoansAsync(Guid branchId, Guid? customerId, int page, CancellationToken ct);
    Task<CylinderLoanDto> LoanAsync(Guid id, CancellationToken ct);
    Task<CylinderLoanDto> ApproveAsync(Guid id, LoanApprovalRequest request, CancellationToken ct);
    Task<CylinderLoanDto> ReturnAsync(Guid id, LoanReturnRequest request, CancellationToken ct);
    Task<CylinderLoanDto> RefundAsync(Guid id, LoanPaymentRequest request, CancellationToken ct);
    Task<CylinderLoanDto> PayAsync(Guid id, LoanPaymentRequest request, CancellationToken ct);
    Task<CylinderLoanDto> ReverseAsync(Guid id, Guid entryId, SaleReverseRequest request, CancellationToken ct);
}
