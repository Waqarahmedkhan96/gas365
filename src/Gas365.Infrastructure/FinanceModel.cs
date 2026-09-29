using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<MoneyAccount> MoneyAccounts => Set<MoneyAccount>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<FinanceEntry> FinanceEntries => Set<FinanceEntry>();
    public DbSet<RegisterEvent> RegisterEvents => Set<RegisterEvent>();
    private void ConfigureFinance(ModelBuilder model)
    {
        var a = model.Entity<MoneyAccount>();
        a.HasKey(x => new { x.TenantId, x.Id }); a.HasQueryFilter(x => x.TenantId == scope.TenantId);
        a.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        a.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        a.HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
        a.HasIndex(x => new { x.TenantId, x.BranchId }).IsUnique().HasFilter("\"DefaultCash\" = TRUE");
        a.Property(x => x.Name).HasMaxLength(120); a.Property(x => x.Kind).HasMaxLength(10); a.Property(x => x.Version).IsRowVersion();
        a.ToTable(t => t.HasCheckConstraint("CK_MoneyAccount_Kind", "\"Kind\" IN ('Cash','Bank') AND (NOT \"DefaultCash\" OR \"Kind\" = 'Cash')"));
        var c = model.Entity<ExpenseCategory>();
        c.HasKey(x => new { x.TenantId, x.Id }); c.HasQueryFilter(x => x.TenantId == scope.TenantId);
        c.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        c.HasIndex(x => new { x.TenantId, x.Name }).IsUnique(); c.Property(x => x.Name).HasMaxLength(120); c.Property(x => x.Version).IsRowVersion();
        var e = model.Entity<FinanceEntry>();
        e.HasKey(x => new { x.TenantId, x.Id }); e.HasQueryFilter(x => x.TenantId == scope.TenantId);
        e.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        e.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.AccountId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ExpenseCategory>().WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<FinanceEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.ParentId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<FinanceEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.ReversesId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<SaleDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.SaleId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<PurchaseDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.PurchaseId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CylinderLoan>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.CylinderLoanId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Sequence).ValueGeneratedOnAdd();
        e.HasIndex(x => new { x.TenantId, x.BranchId, x.Sequence });
        e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.SourceId }).IsUnique().HasFilter("\"SourceId\" IS NOT NULL");
        e.HasIndex(x => new { x.TenantId, x.ReversesId }).IsUnique().HasFilter("\"ReversesId\" IS NOT NULL");
        e.Property(x => x.Amount).HasPrecision(20, 2); e.Property(x => x.Cash).HasPrecision(20, 2);
        e.Property(x => x.Kind).HasMaxLength(40); e.Property(x => x.Reason).HasMaxLength(300); e.Property(x => x.Counterparty).HasMaxLength(120);
        e.Property(x => x.IdempotencyKey).HasMaxLength(80); e.Property(x => x.RequestHash).HasMaxLength(64);
        e.ToTable(t => t.HasCheckConstraint("CK_FinanceEntry_Account", "(\"Cash\" = 0 OR \"AccountId\" IS NOT NULL) AND NOT (\"CustomerId\" IS NOT NULL AND \"SupplierId\" IS NOT NULL)"));
        var r = model.Entity<RegisterEvent>();
        r.HasKey(x => new { x.TenantId, x.Id }); r.HasQueryFilter(x => x.TenantId == scope.TenantId);
        r.HasOne<MoneyAccount>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.AccountId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        r.Property(x => x.Sequence).ValueGeneratedOnAdd();
        r.HasIndex(x => new { x.TenantId, x.AccountId, x.BusinessDate, x.Sequence });
        r.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        r.Property(x => x.Opening).HasPrecision(20, 2); r.Property(x => x.Expected).HasPrecision(20, 2); r.Property(x => x.Counted).HasPrecision(20, 2);
        r.Property(x => x.Kind).HasMaxLength(10); r.Property(x => x.Reason).HasMaxLength(300);
        r.Property(x => x.IdempotencyKey).HasMaxLength(80); r.Property(x => x.RequestHash).HasMaxLength(64);
        r.ToTable(t => t.HasCheckConstraint("CK_RegisterEvent_Kind", "\"Kind\" IN ('Open','Close','Reopen') AND \"Counted\" >= 0"));
    }
}
