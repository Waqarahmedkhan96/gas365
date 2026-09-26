using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<SalePrice> SalePrices => Set<SalePrice>();
    private void ConfigureCustomers(ModelBuilder model)
    {
        void Row<T>() where T : class, ITenantOwned
        {
            var e = model.Entity<T>();
            e.HasKey("TenantId", "Id");
            e.HasQueryFilter(x => x.TenantId == scope.TenantId);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.Property<uint>("Version").IsRowVersion();
        }
        Row<Customer>(); Row<PriceList>(); Row<SalePrice>();
        var customer = model.Entity<Customer>();
        customer.Property(x => x.Name).HasMaxLength(120);
        customer.Property(x => x.Phone).HasMaxLength(80);
        customer.Property(x => x.Address).HasMaxLength(300);
        customer.HasIndex(x => new { x.TenantId, x.Name });
        var list = model.Entity<PriceList>();
        list.Property(x => x.Name).HasMaxLength(120);
        list.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        var price = model.Entity<SalePrice>();
        price.HasOne<PriceList>().WithMany().HasForeignKey(x => new { x.TenantId, x.PriceListId }).OnDelete(DeleteBehavior.Restrict);
        price.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).OnDelete(DeleteBehavior.Restrict);
        price.HasIndex(x => new { x.TenantId, x.PriceListId, x.ItemId, x.Mode }).IsUnique();
        price.Property(x => x.Mode).HasMaxLength(30);
        price.Property(x => x.UnitPrice).HasPrecision(18, 6);
        price.ToTable(t => t.HasCheckConstraint("CK_SalePrice_Range", "\"UnitPrice\" >= 0 AND \"UnitPrice\" <= 1000000"));
    }
}
