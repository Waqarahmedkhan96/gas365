using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseDraft> PurchaseDrafts => Set<PurchaseDraft>();
    public DbSet<PurchaseDocument> PurchaseDocuments => Set<PurchaseDocument>();
    public DbSet<PurchaseEntry> PurchaseEntries => Set<PurchaseEntry>();
    private void ConfigurePurchasing(ModelBuilder model)
    {
        void Row<T>() where T : class, ITenantOwned
        {
            var e = model.Entity<T>();
            e.HasKey("TenantId", "Id");
            e.HasQueryFilter(x => x.TenantId == scope.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        }
        Row<Supplier>(); Row<PurchaseDraft>(); Row<PurchaseDocument>(); Row<PurchaseEntry>();
        var supplier = model.Entity<Supplier>();
        supplier.Property(x => x.Name).HasMaxLength(120); supplier.Property(x => x.Phone).HasMaxLength(80);
        supplier.Property(x => x.Address).HasMaxLength(300); supplier.Property(x => x.Version).IsRowVersion();
        supplier.HasIndex(x => new { x.TenantId, x.Name });
        var draft = model.Entity<PurchaseDraft>();
        draft.Property(x => x.Version).IsRowVersion(); draft.Property(x => x.Content).HasColumnType("jsonb");
        draft.Property(x => x.Snapshot).HasColumnType("jsonb");
        draft.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierId }).OnDelete(DeleteBehavior.Restrict);
        draft.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        var doc = model.Entity<PurchaseDocument>();
        doc.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        doc.HasIndex(x => new { x.TenantId, x.DraftId }).IsUnique();
        doc.HasIndex(x => new { x.TenantId, x.StockDocumentId }).IsUnique();
        doc.HasOne<PurchaseDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.DraftId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<PurchaseDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.CorrectsId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<StockDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.StockDocumentId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierId }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        doc.Property(x => x.Content).HasColumnType("jsonb"); doc.Property(x => x.SupplierName).HasMaxLength(120);
        doc.Property(x => x.Total).HasPrecision(20, 2);
        var entry = model.Entity<PurchaseEntry>();
        entry.HasOne<PurchaseDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.PurchaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        entry.HasOne<PurchaseEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesId }).OnDelete(DeleteBehavior.Restrict);
        entry.HasIndex(x => new { x.TenantId, x.ReversesId }).IsUnique().HasFilter("\"ReversesId\" IS NOT NULL");
        entry.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        entry.HasIndex(x => new { x.TenantId, x.BranchId, x.PostedAt });
        entry.Property(x => x.Payable).HasPrecision(20, 2); entry.Property(x => x.Cash).HasPrecision(20, 2);
        entry.Property(x => x.Kind).HasMaxLength(30); entry.Property(x => x.Reason).HasMaxLength(300);
        entry.Property(x => x.IdempotencyKey).HasMaxLength(80); entry.Property(x => x.RequestHash).HasMaxLength(64);
        entry.ToTable(t => t.HasCheckConstraint("CK_PurchaseEntry_Signs", "(\"Kind\" IN ('Allocation','AllocationReversal') AND \"Cash\" = 0 AND ((\"Kind\" = 'Allocation' AND \"Payable\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Payable\" > 0))) OR (\"Kind\" = 'Invoice' AND \"Payable\" >= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'Payment' AND \"Payable\" < 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'PaymentReversal' AND \"Payable\" > 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'Reversal' AND \"Payable\" <= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'CostPayment' AND \"Payable\" = 0 AND \"Cash\" < 0) OR (\"Kind\" = 'CostPaymentReversal' AND \"Payable\" = 0 AND \"Cash\" > 0)"));
    }
}
