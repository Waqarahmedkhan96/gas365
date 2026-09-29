using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gas365.Application;
using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class FinanceAccess(Gas365DbContext db, TenantScope scope) : IFinanceAccess
{
    private void Require(string permission)
    { if (!scope.Has(Permissions.FinanceView) || !scope.Has(permission)) throw new AccessException(403, "Finance permission required."); }
    private async Task BranchAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == id && b.Active && (scope.IsOwner || scope.Has(Permissions.BranchManage) || db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == id)), ct))
            throw new AccessException(404, "Branch not found.");
    }
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tenants\" WHERE \"TenantId\" = {scope.TenantId} FOR UPDATE", ct);
    private async Task<DateOnly> TodayAsync(CancellationToken ct) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById((await db.Tenants.SingleAsync(ct)).TimeZone)).DateTime);
    private static string Hash(object r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
    private static void Key(string key) { if (CatalogAccess.Text(key, 80) != key || key.StartsWith("internal:", StringComparison.Ordinal)) throw new AccessException(400, "Invalid command key."); }
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    private async Task<FinanceEntry?> ReplayAsync(string key, string hash, CancellationToken ct)
    {
        Key(key); var entry = await db.FinanceEntries.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (entry is not null && entry.RequestHash != hash) throw new AccessException(409, "Command key already used for different contents.");
        return entry;
    }
    private async Task<MoneyAccount> AccountAsync(Guid id, CancellationToken ct)
    {
        var a = await db.MoneyAccounts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Account not found.");
        await BranchAsync(a.BranchId, ct); return a;
    }
    private async Task CheckOpenAsync(MoneyAccount a, DateOnly date, CancellationToken ct)
    {
        if (!a.Active) throw new AccessException(409, "Account " + a.Name + " is retired. Reactivate it before posting.");
        if (a.Kind != "Cash") return;
        var last = await db.RegisterEvents.Where(x => x.AccountId == a.Id && x.BusinessDate == date).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
        if (last is null || last.Kind == "Close") throw new AccessException(409, "Open or reopen the cash register before posting.");
    }
    private async Task<FinanceEntryDto> DtoAsync(FinanceEntry e, CancellationToken ct)
    {
        var remaining = e.Kind is "CustomerReceipt" or "SupplierPayment" or "LoanReceived" or "LoanGiven"
            ? e.Amount + await db.FinanceEntries.Where(x => x.ParentId == e.Id || x.ReversesId == e.Id).SumAsync(x => x.Amount, ct) : 0;
        return new(e.Id, e.Sequence, e.BranchId, e.Kind, e.Amount, e.Cash, e.AccountId, e.CustomerId, e.SupplierId, e.CategoryId,
            e.ParentId, e.ReversesId, e.SaleId, e.PurchaseId, e.Counterparty, e.Reason, e.BusinessDate, e.ActorId, e.PostedAt, remaining, e.CylinderLoanId);
    }
    public async Task<FinancePartyDto[]> PartiesAsync(bool supplier, CancellationToken ct)
    {
        Require(Permissions.FinanceView);
        return supplier ? await db.Suppliers.Where(x => x.Active).OrderBy(x => x.Name).Select(x => new FinancePartyDto(x.Id, x.Name)).ToArrayAsync(ct)
            : await db.Customers.Where(x => x.Active).OrderBy(x => x.Name).Select(x => new FinancePartyDto(x.Id, x.Name)).ToArrayAsync(ct);
    }
    public async Task<FinanceEntryDto[]> OpenItemsAsync(Guid branchId, CancellationToken ct)
    {
        Require(Permissions.FinanceView); await BranchAsync(branchId, ct);
        var rows = await db.FinanceEntries.Where(x => x.BranchId == branchId && (x.Kind == "CustomerReceipt" || x.Kind == "SupplierPayment" || x.Kind == "LoanReceived" || x.Kind == "LoanGiven") &&
            x.Amount + db.FinanceEntries.Where(a => a.ParentId == x.Id || a.ReversesId == x.Id).Sum(a => a.Amount) > 0).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var result = new List<FinanceEntryDto>(); foreach (var row in rows) result.Add(await DtoAsync(row, ct)); return result.ToArray();
    }
    public async Task<MoneyAccountDto[]> AccountsAsync(Guid branchId, CancellationToken ct)
    {
        Require(Permissions.FinanceView); await BranchAsync(branchId, ct);
        return await db.MoneyAccounts.Where(x => x.BranchId == branchId).OrderByDescending(x => x.Active).ThenBy(x => x.Name)
            .Select(x => new MoneyAccountDto(x.Id, x.BranchId, x.Name, x.Kind, x.DefaultCash, db.FinanceEntries.Where(e => e.AccountId == x.Id).Sum(e => e.Cash), x.Active, x.Version)).ToArrayAsync(ct);
    }
    public async Task<MoneyAccountDto> CreateAccountAsync(MoneyAccountRequest r, CancellationToken ct)
    {
        Require(Permissions.FinanceManage); var hash = Hash(r);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct); await BranchAsync(r.BranchId, ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct);
        if (prior is not null) return (await AccountsAsync(r.BranchId, ct)).Single(x => x.Id == prior.AccountId);
        if (r.Kind is not ("Cash" or "Bank")) throw new AccessException(400, "Select Cash or Bank.");
        CatalogAccess.Decimal(r.Opening, 2, 1000000000);
        var a = new MoneyAccount
        {
            TenantId = scope.TenantId,
            BranchId = r.BranchId,
            Name = CatalogAccess.Text(r.Name, 120),
            Kind = r.Kind,
            DefaultCash = r.Kind == "Cash" && !await db.MoneyAccounts.AnyAsync(x => x.BranchId == r.BranchId && x.DefaultCash, ct)
        };
        var e = new FinanceEntry
        {
            TenantId = scope.TenantId,
            BranchId = r.BranchId,
            AccountId = a.Id,
            Kind = "Opening",
            Amount = r.Opening,
            Cash = r.Opening,
            Reason = CatalogAccess.Text(r.Reason, 300),
            IdempotencyKey = r.IdempotencyKey,
            RequestHash = hash,
            ActorId = scope.UserId,
            BusinessDate = await TodayAsync(ct)
        };
        db.AddRange(a, e); Audit("finance.account_opened", a.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (await AccountsAsync(r.BranchId, ct)).Single(x => x.Id == a.Id);
    }
    public async Task<ExpenseCategoryDto[]> CategoriesAsync(CancellationToken ct)
    { Require(Permissions.FinanceView); return await db.ExpenseCategories.OrderByDescending(x => x.Active).ThenBy(x => x.Name).Select(x => new ExpenseCategoryDto(x.Id, x.Name, x.Capital, x.Active, x.Version)).ToArrayAsync(ct); }
    public async Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryRequest r, CancellationToken ct)
    {
        Require(Permissions.FinanceManage);
        var c = new ExpenseCategory { TenantId = scope.TenantId, Name = CatalogAccess.Text(r.Name, 120), Capital = r.Capital };
        db.Add(c); Audit("finance.category_created", c.Id); await db.SaveChangesAsync(ct); return new(c.Id, c.Name, c.Capital, c.Active, c.Version);
    }
    public async Task<MoneyAccountDto> UpdateAccountAsync(Guid id, MasterUpdateRequest r, CancellationToken ct)
    {
        Require(Permissions.FinanceManage);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var a = await AccountAsync(id, ct);
        if (a.Version != r.Version) throw new AccessException(409, "Account changed. Refresh before saving.");
        if (a.Active && !r.Active)
        {
            if (a.DefaultCash) throw new AccessException(409, "The default cash drawer cannot be retired.");
            if (await db.FinanceEntries.Where(x => x.AccountId == a.Id).SumAsync(x => x.Cash, ct) != 0) throw new AccessException(409, "Transfer the balance out before retiring the account.");
            var last = await db.RegisterEvents.Where(x => x.AccountId == a.Id).OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
            if (last is not null && last.Kind != "Close") throw new AccessException(409, "Close the register before retiring the account.");
        }
        var action = a.Active == r.Active ? "finance.account_renamed" : r.Active ? "finance.account_reactivated" : "finance.account_retired";
        a.Name = CatalogAccess.Text(r.Name, 120); a.Active = r.Active;
        Audit(action, a.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return (await AccountsAsync(a.BranchId, ct)).Single(x => x.Id == a.Id);
    }
    public async Task<ExpenseCategoryDto> UpdateCategoryAsync(Guid id, MasterUpdateRequest r, CancellationToken ct)
    {
        Require(Permissions.FinanceManage);
        var c = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Expense category not found.");
        if (c.Version != r.Version) throw new AccessException(409, "Category changed. Refresh before saving.");
        var action = c.Active == r.Active ? "finance.category_renamed" : r.Active ? "finance.category_reactivated" : "finance.category_retired";
        c.Name = CatalogAccess.Text(r.Name, 120); c.Active = r.Active;
        Audit(action, c.Id); await db.SaveChangesAsync(ct); return new(c.Id, c.Name, c.Capital, c.Active, c.Version);
    }
    public async Task<PageDto<FinanceEntryDto>> EntriesAsync(Guid branchId, int page, CancellationToken ct)
    {
        Require(Permissions.FinanceView); await BranchAsync(branchId, ct);
        if (page is < 1 or > 1000000) throw new AccessException(400, "Invalid page.");
        var q = db.FinanceEntries.Where(x => x.BranchId == branchId); var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.Sequence).Skip((page - 1) * 20).Take(20).ToArrayAsync(ct);
        var result = new List<FinanceEntryDto>(); foreach (var e in rows) result.Add(await DtoAsync(e, ct));
        return new(result.ToArray(), count, page, 20);
    }
    public async Task<FinanceEntryDto> PostAsync(FinanceRequest r, CancellationToken ct)
    {
        Require(Permissions.FinancePost); var hash = Hash(r);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct); await BranchAsync(r.BranchId, ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct); if (prior is not null) return await DtoAsync(prior, ct);
        CatalogAccess.Decimal(r.Amount, 2, 1000000000); if (r.Amount <= 0) throw new AccessException(400, "Amount must be positive.");
        var e = new FinanceEntry
        {
            TenantId = scope.TenantId,
            BranchId = r.BranchId,
            Kind = r.Kind,
            Amount = r.Amount,
            AccountId = r.AccountId,
            CustomerId = r.CustomerId,
            SupplierId = r.SupplierId,
            CategoryId = r.CategoryId,
            ParentId = r.ParentId,
            SaleId = r.SaleId,
            PurchaseId = r.PurchaseId,
            CylinderLoanId = r.CylinderLoanId,
            Counterparty = string.IsNullOrWhiteSpace(r.Counterparty) ? "" : CatalogAccess.Text(r.Counterparty, 120),
            Reason = CatalogAccess.Text(r.Reason, 300),
            ActorId = scope.UserId,
            BusinessDate = await TodayAsync(ct),
            IdempotencyKey = r.IdempotencyKey,
            RequestHash = hash
        };
        if (r.CustomerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == r.CustomerId && x.Active, ct) || r.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == r.SupplierId && x.Active, ct)) throw new AccessException(404, "Counterparty not found.");
        if (r.CustomerId.HasValue && r.SupplierId.HasValue) throw new AccessException(400, "Select one counterparty.");
        if (r.Kind != "Allocate" && (r.SaleId.HasValue || r.PurchaseId.HasValue || r.CylinderLoanId.HasValue)) throw new AccessException(400, "Invoice references apply only to allocations.");
        if (r.Kind != "Expense" && r.CategoryId.HasValue) throw new AccessException(400, "Category applies only to expenses.");
        if (r.Kind is not ("Allocate" or "LoanRepayment") && r.ParentId.HasValue) throw new AccessException(400, "Unexpected parent reference.");
        if (r.Kind != "Transfer" && r.DestinationAccountId.HasValue) throw new AccessException(400, "Unexpected destination account.");
        if (r.Kind is not ("CustomerReceipt" or "SupplierPayment" or "Allocate") && (r.CustomerId.HasValue || r.SupplierId.HasValue)) throw new AccessException(400, "Use a receipt or supplier payment for customer/supplier money.");
        switch (r.Kind)
        {
            case "CustomerReceipt":
                if (!r.CustomerId.HasValue) throw new AccessException(400, "Select a customer."); e.Cash = r.Amount; break;
            case "SupplierPayment":
                if (!r.SupplierId.HasValue) throw new AccessException(400, "Select a supplier."); e.Cash = -r.Amount; break;
            case "Expense":
                var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == r.CategoryId, ct) ?? throw new AccessException(404, "Expense category not found.");
                if (!category.Active) throw new AccessException(409, "Expense category is retired."); e.Cash = -r.Amount; break;
            case "CashAdjustmentIn": Require(Permissions.FinanceManage); e.Cash = r.Amount; break;
            case "CashAdjustmentOut": Require(Permissions.FinanceManage); e.Cash = -r.Amount; break;
            case "OwnerFunds": e.Cash = r.Amount; break;
            case "OwnerWithdrawal": e.Cash = -r.Amount; break;
            case "LoanReceived":
            case "LoanGiven":
                e.Counterparty = CatalogAccess.Text(r.Counterparty, 120); e.Cash = r.Kind == "LoanReceived" ? r.Amount : -r.Amount; break;
            case "LoanRepayment":
                var loan = await db.FinanceEntries.SingleOrDefaultAsync(x => x.Id == r.ParentId && x.BranchId == r.BranchId && (x.Kind == "LoanReceived" || x.Kind == "LoanGiven"), ct) ?? throw new AccessException(404, "Loan not found.");
                if ((await DtoAsync(loan, ct)).Unapplied < r.Amount) throw new AccessException(409, "Repayment exceeds outstanding principal.");
                e.Amount = -r.Amount; e.Cash = loan.Kind == "LoanReceived" ? -r.Amount : r.Amount; e.Counterparty = loan.Counterparty; break;
            case "Allocate":
                if (r.AccountId.HasValue) throw new AccessException(400, "Allocation does not move cash.");
                var payment = await db.FinanceEntries.SingleOrDefaultAsync(x => x.Id == r.ParentId && x.BranchId == r.BranchId && (x.Kind == "CustomerReceipt" || x.Kind == "SupplierPayment"), ct) ?? throw new AccessException(404, "Payment not found.");
                if ((await DtoAsync(payment, ct)).Unapplied < r.Amount) throw new AccessException(409, "Allocation exceeds unapplied payment.");
                e.CustomerId = payment.CustomerId; e.SupplierId = payment.SupplierId; e.Amount = -r.Amount;
                await AllocateAsync(e, false, ct); break;
            case "Transfer":
                if (r.AccountId == r.DestinationAccountId || !r.DestinationAccountId.HasValue) throw new AccessException(400, "Select two different accounts.");
                var destination = await AccountAsync(r.DestinationAccountId.Value, ct);
                if (destination.BranchId != r.BranchId) throw new AccessException(400, "Transfer accounts must be in the selected branch.");
                await CheckOpenAsync(destination, e.BusinessDate, ct);
                db.FinanceEntries.Add(new FinanceEntry
                {
                    TenantId = scope.TenantId,
                    BranchId = r.BranchId,
                    AccountId = destination.Id,
                    Kind = "TransferIn",
                    Cash = r.Amount,
                    Amount = r.Amount,
                    ParentId = e.Id,
                    Reason = e.Reason,
                    ActorId = scope.UserId,
                    BusinessDate = e.BusinessDate,
                    IdempotencyKey = "internal:" + e.Id,
                    RequestHash = hash
                });
                e.Cash = -r.Amount; break;
            default: throw new AccessException(400, "Unknown finance action.");
        }
        if (e.Cash != 0)
        {
            var account = await AccountAsync(r.AccountId ?? Guid.Empty, ct);
            if (account.BranchId != r.BranchId) throw new AccessException(404, "Account not found in branch.");
            await CheckOpenAsync(account, e.BusinessDate, ct);
        }
        db.FinanceEntries.Add(e); Audit("finance." + r.Kind.ToLowerInvariant(), e.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await DtoAsync(e, ct);
    }
    private async Task AllocateAsync(FinanceEntry e, bool reverse, CancellationToken ct)
    {
        if (new[] { e.SaleId, e.PurchaseId, e.CylinderLoanId }.Count(x => x.HasValue) != 1) throw new AccessException(400, "Select one invoice matching the payment counterparty.");
        if (e.CustomerId.HasValue && e.SaleId.HasValue)
        {
            var doc = await db.SaleDocuments.SingleOrDefaultAsync(x => x.Id == e.SaleId && x.CustomerId == e.CustomerId && x.BranchId == e.BranchId, ct) ?? throw new AccessException(404, "Customer invoice not found.");
            if (await db.SaleEntries.AnyAsync(x => x.SaleId == doc.Id && x.Kind == "Reversal", ct)) throw new AccessException(409, "Invoice is reversed.");
            if (!reverse && await db.SaleEntries.Where(x => x.SaleId == doc.Id).SumAsync(x => x.Receivable, ct) < -e.Amount) throw new AccessException(409, "Allocation exceeds invoice balance.");
            db.SaleEntries.Add(new SaleEntry
            {
                TenantId = scope.TenantId,
                BranchId = e.BranchId,
                SaleId = doc.Id,
                Kind = reverse ? "AllocationReversal" : "Allocation",
                Receivable = e.Amount,
                ActorId = scope.UserId,
                Reason = e.Reason,
                PostedAt = e.PostedAt,
                BusinessDate = e.BusinessDate,
                IdempotencyKey = "internal:" + e.Id,
                RequestHash = e.RequestHash
            });
        }
        else if (e.SupplierId.HasValue && e.PurchaseId.HasValue)
        {
            var doc = await db.PurchaseDocuments.SingleOrDefaultAsync(x => x.Id == e.PurchaseId && x.SupplierId == e.SupplierId && x.BranchId == e.BranchId, ct) ?? throw new AccessException(404, "Supplier invoice not found.");
            if (await db.PurchaseEntries.AnyAsync(x => x.PurchaseId == doc.Id && x.Kind == "Reversal", ct)) throw new AccessException(409, "Invoice is reversed.");
            if (!reverse && await db.PurchaseEntries.Where(x => x.PurchaseId == doc.Id).SumAsync(x => x.Payable, ct) < -e.Amount) throw new AccessException(409, "Allocation exceeds invoice balance.");
            db.PurchaseEntries.Add(new PurchaseEntry
            {
                TenantId = scope.TenantId,
                BranchId = e.BranchId,
                PurchaseId = doc.Id,
                Kind = reverse ? "AllocationReversal" : "Allocation",
                Payable = e.Amount,
                ActorId = scope.UserId,
                Reason = e.Reason,
                PostedAt = e.PostedAt,
                BusinessDate = e.BusinessDate,
                IdempotencyKey = "internal:" + e.Id,
                RequestHash = e.RequestHash
            });
        }
        else if (e.CustomerId.HasValue && e.CylinderLoanId.HasValue)
        {
            var loan = await db.CylinderLoans.SingleOrDefaultAsync(x => x.Id == e.CylinderLoanId && x.CustomerId == e.CustomerId && x.BranchId == e.BranchId, ct) ?? throw new AccessException(404, "Cylinder settlement not found.");
            var rows = await db.CylinderLoanEntries.Where(x => x.LoanId == loan.Id).ToArrayAsync(ct);
            if (!rows.Any(x => x.Kind == "Return" && !rows.Any(y => y.ReversesId == x.Id))) throw new AccessException(409, "Return the cylinder before allocating to its charges.");
            if (!reverse && rows.Sum(x => x.Receivable) < -e.Amount) throw new AccessException(409, "Allocation exceeds the cylinder amount due.");
            db.CylinderLoanEntries.Add(new CylinderLoanEntry
            {
                TenantId = scope.TenantId,
                BranchId = e.BranchId,
                LoanId = loan.Id,
                Kind = reverse ? "AllocationReversal" : "Allocation",
                Receivable = e.Amount,
                ActorId = scope.UserId,
                Reason = e.Reason,
                PostedAt = e.PostedAt,
                BusinessDate = e.BusinessDate,
                ReversesId = reverse ? rows.Single(x => x.IdempotencyKey == "internal:" + e.ReversesId).Id : null,
                IdempotencyKey = "internal:" + e.Id,
                RequestHash = e.RequestHash
            });
        }
        else throw new AccessException(400, "Select one invoice matching the payment counterparty.");
    }
    public async Task<FinanceEntryDto> ReverseAsync(Guid id, FinanceReverseRequest r, CancellationToken ct)
    {
        Require(Permissions.FinanceReverse); var hash = Hash(new { id, r });
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var prior = await ReplayAsync(r.IdempotencyKey, hash, ct); if (prior is not null) { await BranchAsync(prior.BranchId, ct); return await DtoAsync(prior, ct); }
        var original = await db.FinanceEntries.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Entry not found."); await BranchAsync(original.BranchId, ct);
        if (original.Kind is "CashAdjustmentIn" or "CashAdjustmentOut") Require(Permissions.FinanceManage);
        if (original.Kind is "Opening" or "TransferIn" || original.SourceId.HasValue || original.ReversesId.HasValue) throw new AccessException(409, "Reverse this entry through its originating document.");
        if (await db.FinanceEntries.AnyAsync(x => x.ReversesId == id, ct)) throw new AccessException(409, "Entry already reversed.");
        if (original.Kind != "Transfer" && await db.FinanceEntries.Where(x => x.ParentId == id).SumAsync(x => x.Amount, ct) != 0) throw new AccessException(409, "Reverse allocations or repayments first.");
        var e = new FinanceEntry
        {
            TenantId = scope.TenantId,
            BranchId = original.BranchId,
            Kind = original.Kind + "Reversal",
            Amount = -original.Amount,
            Cash = -original.Cash,
            AccountId = original.AccountId,
            CustomerId = original.CustomerId,
            SupplierId = original.SupplierId,
            CategoryId = original.CategoryId,
            ParentId = original.ParentId,
            SaleId = original.SaleId,
            PurchaseId = original.PurchaseId,
            CylinderLoanId = original.CylinderLoanId,
            Counterparty = original.Counterparty,
            ReversesId = id,
            ActorId = scope.UserId,
            BusinessDate = await TodayAsync(ct),
            Reason = CatalogAccess.Text(r.Reason, 300),
            IdempotencyKey = r.IdempotencyKey,
            RequestHash = hash
        };
        if (e.AccountId.HasValue) await CheckOpenAsync(await AccountAsync(e.AccountId.Value, ct), e.BusinessDate, ct);
        if (original.Kind == "Allocate") await AllocateAsync(e, true, ct);
        if (original.Kind == "Transfer")
        {
            var incoming = await db.FinanceEntries.SingleAsync(x => x.ParentId == id && x.Kind == "TransferIn", ct);
            await CheckOpenAsync(await AccountAsync(incoming.AccountId!.Value, ct), e.BusinessDate, ct);
            db.FinanceEntries.Add(new FinanceEntry
            {
                TenantId = scope.TenantId,
                BranchId = e.BranchId,
                AccountId = incoming.AccountId,
                Kind = "TransferInReversal",
                Cash = -incoming.Cash,
                Amount = -incoming.Amount,
                ParentId = e.Id,
                ReversesId = incoming.Id,
                Reason = e.Reason,
                ActorId = scope.UserId,
                BusinessDate = e.BusinessDate,
                IdempotencyKey = "internal:" + e.Id,
                RequestHash = hash
            });
        }
        db.Add(e); Audit("finance.reversed", e.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await DtoAsync(e, ct);
    }
    public async Task<RegisterDto> RegisterAsync(Guid accountId, DateOnly date, CancellationToken ct)
    {
        Require(Permissions.FinanceView); var a = await AccountAsync(accountId, ct);
        if (a.Kind != "Cash") throw new AccessException(400, "Cash registers require a cash account.");
        var rows = await db.RegisterEvents.Where(x => x.AccountId == accountId && x.BusinessDate == date).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var opening = rows.FirstOrDefault()?.Opening ?? await db.FinanceEntries.Where(x => x.AccountId == accountId && (x.BusinessDate < date || x.BusinessDate == date && x.Kind == "Opening")).SumAsync(x => x.Cash, ct);
        var expected = FinanceCalculation.ExpectedClose(opening, await db.FinanceEntries.Where(x => x.AccountId == accountId && x.BusinessDate == date && x.Kind != "Opening").Select(x => x.Cash).ToArrayAsync(ct));
        return new(accountId, date, rows.LastOrDefault()?.Kind == "Close" ? "Closed" : rows.Length == 0 ? "Not opened" : "Open", opening, expected, rows.LastOrDefault()?.Id,
            rows.Select(x => new RegisterEventDto(x.Id, x.Kind, x.BusinessDate, x.Opening, x.Expected, x.Counted, x.Counted - x.Expected, x.Reason, x.ActorId, x.PostedAt)).ToArray());
    }
    public async Task<RegisterDto> RegisterActionAsync(RegisterRequestDto r, CancellationToken ct)
    {
        Require(r.Kind == "Reopen" ? Permissions.RegisterReopen : r.Kind == "Close" ? Permissions.RegisterClose : Permissions.FinancePost);
        Key(r.IdempotencyKey); var hash = Hash(r);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var a = await AccountAsync(r.AccountId, ct);
        var prior = await db.RegisterEvents.SingleOrDefaultAsync(x => x.IdempotencyKey == r.IdempotencyKey, ct);
        if (prior is not null) { if (prior.RequestHash != hash) throw new AccessException(409, "Command key conflict."); return await RegisterAsync(a.Id, r.BusinessDate, ct); }
        if (r.BusinessDate > await TodayAsync(ct) || r.BusinessDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(a.CreatedAt, TimeZoneInfo.FindSystemTimeZoneById((await db.Tenants.SingleAsync(ct)).TimeZone)).DateTime)) throw new AccessException(400, "Register date is outside account history.");
        var current = await RegisterAsync(a.Id, r.BusinessDate, ct);
        if (current.LastEventId != r.ExpectedLastEventId) throw new AccessException(409, "Register changed. Refresh first.");
        if (!(r.Kind == "Open" && current.Status == "Not opened" || r.Kind == "Close" && current.Status == "Open" || r.Kind == "Reopen" && current.Status == "Closed")) throw new AccessException(409, "Invalid register transition.");
        if (r.Kind == "Open")
        {
            var dates = await db.RegisterEvents.Where(x => x.AccountId == a.Id).GroupBy(x => x.BusinessDate).Select(g => g.OrderByDescending(x => x.Sequence).First()).ToArrayAsync(ct);
            if (dates.Any(x => x.Kind != "Close") || dates.Any(x => x.BusinessDate >= r.BusinessDate)) throw new AccessException(409, "Close earlier sessions and open dates in order.");
        }
        if (!a.Active) throw new AccessException(409, "Account is retired.");
        if (r.Kind == "Reopen" && await db.RegisterEvents.AnyAsync(x => x.AccountId == a.Id && x.BusinessDate > r.BusinessDate, ct)) throw new AccessException(409, "A later register exists; use a current-date correction to preserve its opening.");
        CatalogAccess.Decimal(r.Counted, 2, 1000000000);
        var e = new RegisterEvent
        {
            TenantId = scope.TenantId,
            BranchId = a.BranchId,
            AccountId = a.Id,
            BusinessDate = r.BusinessDate,
            Kind = r.Kind,
            Opening = current.Opening,
            Expected = current.Expected,
            Counted = r.Counted,
            Reason = CatalogAccess.Text(r.Reason, 300),
            ActorId = scope.UserId,
            IdempotencyKey = r.IdempotencyKey,
            RequestHash = hash
        };
        db.Add(e); Audit("register." + r.Kind.ToLowerInvariant(), e.Id); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await RegisterAsync(a.Id, r.BusinessDate, ct);
    }
    public async Task<CreditDto> CreditAsync(Guid branchId, Guid partyId, bool supplier, DateOnly asOf, CancellationToken ct)
    {
        Require(Permissions.FinanceView); await BranchAsync(branchId, ct);
        if (supplier ? !await db.Suppliers.AnyAsync(x => x.Id == partyId, ct) : !await db.Customers.AnyAsync(x => x.Id == partyId, ct)) throw new AccessException(404, "Counterparty not found.");
        var lines = new List<CreditLineDto>(); var ledger = new List<CreditLedgerLineDto>();
        if (supplier)
        {
            var docs = await db.PurchaseDocuments.Where(x => x.BranchId == branchId && x.SupplierId == partyId).ToArrayAsync(ct);
            var ids = docs.Select(x => x.Id).ToArray(); var entries = await db.PurchaseEntries.Where(x => ids.Contains(x.PurchaseId) && x.BusinessDate <= asOf).ToArrayAsync(ct);
            foreach (var doc in docs)
            {
                var rows = entries.Where(x => x.PurchaseId == doc.Id).ToArray(); var invoice = rows.SingleOrDefault(x => x.Kind == "Invoice");
                if (invoice is not null) lines.Add(new(doc.Id, doc.SupplierName + " / " + doc.Id.ToString()[..8], invoice.BusinessDate, doc.Total, rows.Sum(x => x.Payable), FinanceCalculation.AgingBucket(invoice.BusinessDate, asOf), "Purchase"));
            }
            ledger.AddRange(entries.Select(x => new CreditLedgerLineDto(x.Id, x.Kind, x.BusinessDate, x.Payable, x.Reason)));
        }
        else
        {
            var docs = await db.SaleDocuments.Where(x => x.BranchId == branchId && x.CustomerId == partyId).ToArrayAsync(ct);
            var ids = docs.Select(x => x.Id).ToArray(); var entries = await db.SaleEntries.Where(x => ids.Contains(x.SaleId) && x.BusinessDate <= asOf).ToArrayAsync(ct);
            foreach (var doc in docs)
            {
                var rows = entries.Where(x => x.SaleId == doc.Id).ToArray(); var invoice = rows.SingleOrDefault(x => x.Kind == "Invoice");
                if (invoice is not null) lines.Add(new(doc.Id, doc.Reference, invoice.BusinessDate, doc.Total, rows.Sum(x => x.Receivable), FinanceCalculation.AgingBucket(invoice.BusinessDate, asOf), "Sale"));
            }
            ledger.AddRange(entries.Select(x => new CreditLedgerLineDto(x.Id, x.Kind, x.BusinessDate, x.Receivable, x.Reason)));
            var loans = await db.CylinderLoans.Where(x => x.BranchId == branchId && x.CustomerId == partyId).Select(x => x.Id).ToArrayAsync(ct);
            var loanEntries = await db.CylinderLoanEntries.Where(x => loans.Contains(x.LoanId) && x.BusinessDate <= asOf).ToArrayAsync(ct);
            ledger.AddRange(loanEntries.Select(x => new CreditLedgerLineDto(x.Id, "Cylinder " + x.Kind, x.BusinessDate, x.Receivable, x.Reason)));
            foreach (var loan in loans)
            {
                var rows = loanEntries.Where(x => x.LoanId == loan).ToArray(); var due = rows.Where(x => x.Receivable > 0).OrderBy(x => x.Sequence).FirstOrDefault();
                if (due is not null) lines.Add(new(loan, "Cylinder settlement", due.BusinessDate, due.Receivable, rows.Sum(x => x.Receivable), FinanceCalculation.AgingBucket(due.BusinessDate, asOf), "CylinderLoan"));
            }
        }
        var money = await db.FinanceEntries.Where(x => x.BranchId == branchId && (supplier ? x.SupplierId == partyId : x.CustomerId == partyId) && x.BusinessDate <= asOf).ToArrayAsync(ct);
        var unapplied = money.Sum(x => x.Amount);
        // Allocation entries in invoice journals already reduce their balance. Their finance counterpart releases unapplied credit.
        ledger.AddRange(money.Select(x => new CreditLedgerLineDto(x.Id, x.Kind, x.BusinessDate, -x.Amount, x.Reason)));
        return new(ledger.Sum(x => x.BalanceChange), unapplied, lines.ToArray(), ledger.OrderBy(x => x.Date).ThenBy(x => x.Id).ToArray());
    }
}
