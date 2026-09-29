using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();
    private void ConfigureReporting(ModelBuilder model)
    {
        var job = model.Entity<ExportJob>();
        job.HasKey(x => new { x.TenantId, x.Id }); job.HasQueryFilter(x => x.TenantId == scope.TenantId);
        job.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).OnDelete(DeleteBehavior.Restrict);
        job.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        job.HasIndex(x => new { x.TenantId, x.MembershipId, x.RequestedAt });
        job.HasIndex(x => new { x.Status, x.RequestedAt });
        job.Property(x => x.ReportKey).HasMaxLength(40); job.Property(x => x.Format).HasMaxLength(10); job.Property(x => x.Status).HasMaxLength(20);
        job.Property(x => x.Filter).HasMaxLength(400); job.Property(x => x.Error).HasMaxLength(300); job.Property(x => x.FileName).HasMaxLength(120);
        job.Property(x => x.ContentType).HasMaxLength(100); job.Property(x => x.IdempotencyKey).HasMaxLength(80); job.Property(x => x.RequestHash).HasMaxLength(64);
        job.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ExportJob_Format", "\"Format\" IN ('csv','xlsx','pdf')");
            t.HasCheckConstraint("CK_ExportJob_Status", "\"Status\" IN ('Queued','Running','Completed','Failed','Expired') AND \"Attempts\" >= 0 AND (\"Status\" <> 'Completed' OR \"Content\" IS NOT NULL)");
        });
        // Reports scan ledgers by branch and business date.
        model.Entity<SaleEntry>().HasIndex(x => new { x.TenantId, x.BranchId, x.BusinessDate });
        model.Entity<PurchaseEntry>().HasIndex(x => new { x.TenantId, x.BranchId, x.BusinessDate });
        model.Entity<FinanceEntry>().HasIndex(x => new { x.TenantId, x.BranchId, x.BusinessDate });
        model.Entity<CylinderLoanEntry>().HasIndex(x => new { x.TenantId, x.BranchId, x.BusinessDate });
        model.Entity<StockDocument>().HasIndex(x => new { x.TenantId, x.BranchId, x.BusinessDate });
    }
}
