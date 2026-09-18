using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<StockUnit> StockUnits => Set<StockUnit>();
    public DbSet<StockCategory> StockCategories => Set<StockCategory>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<CylinderAsset> CylinderAssets => Set<CylinderAsset>();
    private void ConfigureInventory(ModelBuilder model)
    {
        void TenantRow<T>() where T : class, ITenantOwned
        {
            var e = model.Entity<T>();
            e.HasKey("TenantId", "Id");
            e.HasQueryFilter(x => x.TenantId == scope.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        }
        TenantRow<StockUnit>(); TenantRow<StockCategory>(); TenantRow<StockItem>(); TenantRow<Warehouse>();
        TenantRow<StockDocument>(); TenantRow<StockMovement>(); TenantRow<CylinderAsset>();
        model.Entity<StockUnit>().Property(x => x.Code).HasMaxLength(20);
        model.Entity<StockUnit>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        model.Entity<StockUnit>().ToTable(t => t.HasCheckConstraint("CK_Unit_Precision", "\"DecimalPlaces\" BETWEEN 0 AND 3"));
        model.Entity<StockCategory>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<StockCategory>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        var item = model.Entity<StockItem>();
        item.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        item.Property(x => x.Sku).HasMaxLength(40); item.Property(x => x.Name).HasMaxLength(120);
        item.Property(x => x.Kind).HasMaxLength(20); item.Property(x => x.Brand).HasMaxLength(120); item.Property(x => x.Classification).HasMaxLength(20);
        item.Property(x => x.CapacityKg).HasPrecision(18, 3); item.Property(x => x.TareWeightKg).HasPrecision(18, 3); item.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        item.Property(x => x.Version).IsRowVersion();
        item.HasOne<StockUnit>().WithMany().HasForeignKey(x => new { x.TenantId, x.UnitId }).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<StockCategory>().WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId }).OnDelete(DeleteBehavior.Restrict);
        item.ToTable(t => t.HasCheckConstraint("CK_Item_Kind", "\"Kind\" IN ('Lpg','Cylinder','SparePart') AND \"ReorderLevel\" >= 0 AND (NOT \"SerialTracked\" OR \"Kind\" = 'Cylinder')"));
        var warehouse = model.Entity<Warehouse>();
        warehouse.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        warehouse.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        warehouse.HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
        warehouse.Property(x => x.Name).HasMaxLength(120); warehouse.Property(x => x.Location).HasMaxLength(200); warehouse.Property(x => x.Version).IsRowVersion();
        var document = model.Entity<StockDocument>();
        document.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        document.HasIndex(x => new { x.TenantId, x.ReversesId }).IsUnique().HasFilter("\"ReversesId\" IS NOT NULL");
        document.HasOne<StockDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesId }).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        document.Property(x => x.IdempotencyKey).HasMaxLength(80); document.Property(x => x.RequestHash).HasMaxLength(64);
        document.Property(x => x.Kind).HasMaxLength(30); document.Property(x => x.Reason).HasMaxLength(300);
        var movement = model.Entity<StockMovement>();
        movement.Property(x => x.Sequence).UseIdentityAlwaysColumn();
        movement.HasIndex(x => new { x.TenantId, x.Sequence }).IsUnique();
        movement.HasIndex(x => new { x.TenantId, x.ItemId, x.WarehouseId, x.Sequence });
        movement.HasOne<StockDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId }).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<Warehouse>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.WarehouseId })
            .HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        movement.Property(x => x.Quantity).HasPrecision(18, 3); movement.Property(x => x.Value).HasPrecision(20, 2); movement.Property(x => x.UnitCost).HasPrecision(20, 6);
        movement.Property(x => x.Bucket).HasMaxLength(30);
        movement.ToTable(t => t.HasCheckConstraint("CK_Movement_Signs", "\"Quantity\" <> 0 AND \"UnitCost\" >= 0 AND ((\"Quantity\" > 0 AND \"Value\" >= 0) OR (\"Quantity\" < 0 AND \"Value\" <= 0))"));
        var asset = model.Entity<CylinderAsset>();
        asset.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).OnDelete(DeleteBehavior.Restrict);
        asset.HasIndex(x => new { x.TenantId, x.Serial }).IsUnique();
        asset.Property(x => x.Serial).HasMaxLength(80); asset.Property(x => x.Owner).HasMaxLength(120); asset.Property(x => x.InspectionReference).HasMaxLength(120);
        asset.Property(x => x.Version).IsRowVersion();
    }
}
