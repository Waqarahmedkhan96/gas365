using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<CylinderPolicy> CylinderPolicies => Set<CylinderPolicy>();
    public DbSet<CustomerDepositArrangement> CustomerDepositArrangements => Set<CustomerDepositArrangement>();
    public DbSet<CylinderLoan> CylinderLoans => Set<CylinderLoan>();
    public DbSet<CylinderLoanEntry> CylinderLoanEntries => Set<CylinderLoanEntry>();
    private void ConfigureCylinderLoans(ModelBuilder model)
    {
        void Row<T>() where T : class, ITenantOwned
        {
            var e = model.Entity<T>(); e.HasKey("TenantId", "Id"); e.HasQueryFilter(x => x.TenantId == scope.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        }
        Row<CylinderPolicy>(); Row<CustomerDepositArrangement>(); Row<CylinderLoan>(); Row<CylinderLoanEntry>();
        var policy = model.Entity<CylinderPolicy>();
        policy.HasIndex(x => new { x.TenantId, x.ItemId }).IsUnique(); policy.Property(x => x.Version).IsRowVersion();
        policy.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).OnDelete(DeleteBehavior.Restrict);
        policy.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.GasItemId }).OnDelete(DeleteBehavior.Restrict);
        policy.Property(x => x.GasQuantity).HasPrecision(18, 3); policy.Property(x => x.SuggestedDeposit).HasPrecision(20, 2); policy.Property(x => x.UsageTerms).HasMaxLength(300);
        policy.ToTable(t => t.HasCheckConstraint("CK_CylinderPolicy_Amounts", "\"GasQuantity\" > 0 AND \"GasQuantity\" <= 1000000 AND \"SuggestedDeposit\" >= 0 AND \"SuggestedDeposit\" <= 1000000000"));
        var arrangement = model.Entity<CustomerDepositArrangement>();
        arrangement.HasIndex(x => new { x.TenantId, x.CustomerId }).IsUnique(); arrangement.Property(x => x.Version).IsRowVersion();
        arrangement.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict); arrangement.Property(x => x.Reason).HasMaxLength(300);
        var loan = model.Entity<CylinderLoan>();
        loan.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        loan.HasOne<SaleDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.SaleId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        loan.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
        loan.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).OnDelete(DeleteBehavior.Restrict);
        loan.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.WarehouseId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        loan.Property(x => x.Deposit).HasPrecision(20, 2); loan.Property(x => x.Serial).HasMaxLength(80); loan.Property(x => x.UsageTerms).HasMaxLength(300);
        loan.ToTable(t => t.HasCheckConstraint("CK_CylinderLoan_Deposit", "\"Deposit\" >= 0 AND \"Deposit\" <= 1000000000"));
        var e = model.Entity<CylinderLoanEntry>();
        e.HasOne<CylinderLoan>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.LoanId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StockDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.StockDocumentId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CylinderLoanEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CylinderLoanEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.ApprovalId }).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.ReversesId }).IsUnique().HasFilter("\"ReversesId\" IS NOT NULL");
        e.Property(x => x.Sequence).ValueGeneratedOnAdd(); e.HasIndex(x => new { x.TenantId, x.Sequence }).IsUnique();
        foreach (var property in new[] { "Cash", "Liability", "Receivable", "Revenue", "UsageFee", "DamageOrLossCharge" }) e.Property<decimal>(property).HasPrecision(20, 2);
        e.Property(x => x.Kind).HasMaxLength(30); e.Property(x => x.Condition).HasMaxLength(30); e.Property(x => x.Reason).HasMaxLength(300);
        e.Property(x => x.IdempotencyKey).HasMaxLength(80); e.Property(x => x.RequestHash).HasMaxLength(64);
        // Advance allocations settle the receivable from customer credit held in finance entries, so only the receivable moves.
        e.ToTable(t => t.HasCheckConstraint("CK_CylinderLoanEntry_Reconcile", "(\"Kind\" IN ('Allocation','AllocationReversal') AND \"Cash\" = 0 AND \"Revenue\" = 0 AND \"Liability\" = 0 AND \"UsageFee\" = 0 AND \"DamageOrLossCharge\" = 0 AND ((\"Kind\" = 'Allocation' AND \"Receivable\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Receivable\" > 0))) OR (\"Kind\" NOT IN ('Allocation','AllocationReversal') AND \"Cash\" + \"Receivable\" = \"Revenue\" + \"Liability\")"));
    }
}
