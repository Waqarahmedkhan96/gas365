namespace Gas365.Application;

public sealed record MoneyAccountRequest(Guid BranchId, string Name, string Kind, decimal Opening, string Reason, string IdempotencyKey);
public sealed record MoneyAccountDto(Guid Id, Guid BranchId, string Name, string Kind, bool DefaultCash, decimal Balance);
public sealed record ExpenseCategoryRequest(string Name, bool Capital);
public sealed record ExpenseCategoryDto(Guid Id, string Name, bool Capital);
public sealed record FinanceRequest(Guid BranchId, string Kind, decimal Amount, string Reason, string IdempotencyKey,
    Guid? AccountId = null, Guid? CustomerId = null, Guid? SupplierId = null, Guid? CategoryId = null,
    Guid? ParentId = null, Guid? SaleId = null, Guid? PurchaseId = null, Guid? DestinationAccountId = null, string Counterparty = "");
public sealed record FinanceReverseRequest(string Reason, string IdempotencyKey);
public sealed record FinanceEntryDto(Guid Id, long Sequence, Guid BranchId, string Kind, decimal Amount, decimal Cash, Guid? AccountId,
    Guid? CustomerId, Guid? SupplierId, Guid? CategoryId, Guid? ParentId, Guid? ReversesId, Guid? SaleId, Guid? PurchaseId,
    string Counterparty, string Reason, DateOnly BusinessDate, Guid ActorId, DateTimeOffset PostedAt, decimal Unapplied);
public sealed record RegisterRequestDto(Guid AccountId, DateOnly BusinessDate, string Kind, decimal Counted, string Reason, string IdempotencyKey, Guid? ExpectedLastEventId);
public sealed record RegisterEventDto(Guid Id, string Kind, DateOnly BusinessDate, decimal Opening, decimal Expected, decimal Counted, decimal Variance, string Reason, Guid ActorId, DateTimeOffset PostedAt);
public sealed record RegisterDto(Guid AccountId, DateOnly BusinessDate, string Status, decimal Opening, decimal Expected, Guid? LastEventId, RegisterEventDto[] History);
public sealed record CreditLineDto(Guid Id, string Reference, DateOnly Date, decimal Original, decimal Outstanding, string Bucket);
public sealed record CreditLedgerLineDto(Guid Id, string Kind, DateOnly Date, decimal BalanceChange, string Reason);
public sealed record CreditDto(decimal Balance, decimal Unapplied, CreditLineDto[] Invoices, CreditLedgerLineDto[] Entries);
public sealed record FinancePartyDto(Guid Id, string Name);
public interface IFinanceAccess
{
    Task<FinancePartyDto[]> PartiesAsync(bool supplier, CancellationToken ct);
    Task<FinanceEntryDto[]> OpenItemsAsync(Guid branchId, CancellationToken ct);
    Task<MoneyAccountDto[]> AccountsAsync(Guid branchId, CancellationToken ct);
    Task<MoneyAccountDto> CreateAccountAsync(MoneyAccountRequest r, CancellationToken ct);
    Task<ExpenseCategoryDto[]> CategoriesAsync(CancellationToken ct);
    Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryRequest r, CancellationToken ct);
    Task<PageDto<FinanceEntryDto>> EntriesAsync(Guid branchId, int page, CancellationToken ct);
    Task<FinanceEntryDto> PostAsync(FinanceRequest r, CancellationToken ct);
    Task<FinanceEntryDto> ReverseAsync(Guid id, FinanceReverseRequest r, CancellationToken ct);
    Task<RegisterDto> RegisterAsync(Guid accountId, DateOnly date, CancellationToken ct);
    Task<RegisterDto> RegisterActionAsync(RegisterRequestDto r, CancellationToken ct);
    Task<CreditDto> CreditAsync(Guid branchId, Guid partyId, bool supplier, DateOnly asOf, CancellationToken ct);
}
