using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<SaleDocument> SaleDocuments => Set<SaleDocument>();
    public DbSet<SaleEntry> SaleEntries => Set<SaleEntry>();
    private void ConfigurePostedSales(ModelBuilder model)
    {
        var doc = model.Entity<SaleDocument>();
        doc.HasKey(x => new { x.TenantId, x.Id });
        doc.HasQueryFilter(x => x.TenantId == scope.TenantId);
        doc.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        doc.HasIndex(x => new { x.TenantId, x.DraftId }).IsUnique();
        doc.HasIndex(x => new { x.TenantId, x.StockDocumentId }).IsUnique();
        doc.HasOne<SaleDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.DraftId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<StockDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.StockDocumentId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        doc.Property(x => x.Content).HasColumnType("jsonb"); doc.Property(x => x.Reference).HasMaxLength(120);
        doc.Property(x => x.Total).HasPrecision(20, 2); doc.Property(x => x.Cogs).HasPrecision(20, 2);
        doc.ToTable(t => t.HasCheckConstraint("CK_SaleDocument_Amounts", "\"Total\" >= 0 AND \"Cogs\" >= 0"));
        var e = model.Entity<SaleEntry>();
        e.HasKey(x => new { x.TenantId, x.Id });
        e.HasQueryFilter(x => x.TenantId == scope.TenantId);
        e.HasOne<SaleDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.SaleId })
            .HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<SaleEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesId }).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Sequence).ValueGeneratedOnAdd();
        e.HasIndex(x => new { x.TenantId, x.Sequence }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.ReversesId }).IsUnique().HasFilter("\"ReversesId\" IS NOT NULL");
        e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        e.Property(x => x.Receivable).HasPrecision(20, 2); e.Property(x => x.Cash).HasPrecision(20, 2);
        e.Property(x => x.Revenue).HasPrecision(20, 2); e.Property(x => x.Cogs).HasPrecision(20, 2);
        e.Property(x => x.Kind).HasMaxLength(30); e.Property(x => x.Reason).HasMaxLength(300);
        e.Property(x => x.IdempotencyKey).HasMaxLength(80); e.Property(x => x.RequestHash).HasMaxLength(64);
        e.ToTable(t => t.HasCheckConstraint("CK_SaleEntry_Signs", "(\"Kind\" = 'Invoice' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" >= 0 AND \"Cash\" = 0 AND \"Cogs\" >= 0) OR (\"Kind\" = 'Payment' AND \"Cash\" > 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'PaymentReversal' AND \"Cash\" < 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'Reversal' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" <= 0 AND \"Cash\" = 0 AND \"Cogs\" <= 0)"));
    }
}
