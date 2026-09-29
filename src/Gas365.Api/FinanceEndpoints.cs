using Gas365.Application;

namespace Gas365.Api;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/workspace/finance").WithTags("Finance and cash registers").RequireAuthorization();
        g.MapGet("/parties", (bool supplier, IFinanceAccess s, CancellationToken ct) => s.PartiesAsync(supplier, ct));
        g.MapGet("/open-items", (Guid branchId, IFinanceAccess s, CancellationToken ct) => s.OpenItemsAsync(branchId, ct));
        g.MapGet("/accounts", (Guid branchId, IFinanceAccess s, CancellationToken ct) => s.AccountsAsync(branchId, ct));
        g.MapPost("/accounts", (MoneyAccountRequest r, IFinanceAccess s, CancellationToken ct) => s.CreateAccountAsync(r, ct));
        g.MapPut("/accounts/{id:guid}", (Guid id, MasterUpdateRequest r, IFinanceAccess s, CancellationToken ct) => s.UpdateAccountAsync(id, r, ct));
        g.MapGet("/categories", (IFinanceAccess s, CancellationToken ct) => s.CategoriesAsync(ct));
        g.MapPost("/categories", (ExpenseCategoryRequest r, IFinanceAccess s, CancellationToken ct) => s.CreateCategoryAsync(r, ct));
        g.MapPut("/categories/{id:guid}", (Guid id, MasterUpdateRequest r, IFinanceAccess s, CancellationToken ct) => s.UpdateCategoryAsync(id, r, ct));
        g.MapGet("/entries", (Guid branchId, IFinanceAccess s, CancellationToken ct, int page = 1) => s.EntriesAsync(branchId, page, ct));
        g.MapPost("/entries", (FinanceRequest r, IFinanceAccess s, CancellationToken ct) => s.PostAsync(r, ct));
        g.MapPost("/entries/{id:guid}/reverse", (Guid id, FinanceReverseRequest r, IFinanceAccess s, CancellationToken ct) => s.ReverseAsync(id, r, ct));
        g.MapPost("/openings", (OpeningBalanceRequest r, IFinanceAccess s, CancellationToken ct) => s.CreateOpeningAsync(r, ct));
        g.MapPost("/openings/{id:guid}/reverse", (Guid id, FinanceReverseRequest r, IFinanceAccess s, CancellationToken ct) => s.ReverseOpeningAsync(id, r, ct));
        g.MapGet("/register", (Guid accountId, DateOnly date, IFinanceAccess s, CancellationToken ct) => s.RegisterAsync(accountId, date, ct));
        g.MapPost("/register", (RegisterRequestDto r, IFinanceAccess s, CancellationToken ct) => s.RegisterActionAsync(r, ct));
        g.MapGet("/credit", (Guid branchId, Guid partyId, bool supplier, DateOnly asOf, IFinanceAccess s, CancellationToken ct) => s.CreditAsync(branchId, partyId, supplier, asOf, ct));
    }
}
