using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<SaleDraft> SaleDrafts => Set<SaleDraft>();
    private void ConfigureSales(ModelBuilder model)
    {
        var d = model.Entity<SaleDraft>();
        d.HasKey(x => new { x.TenantId, x.Id });
        d.HasQueryFilter(x => x.TenantId == scope.TenantId);
        d.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        d.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
        d.HasOne<PriceList>().WithMany().HasForeignKey(x => new { x.TenantId, x.PriceListId }).OnDelete(DeleteBehavior.Restrict);
        d.Property(x => x.Reference).HasMaxLength(120);
        d.Property(x => x.Content).HasColumnType("jsonb"); d.Property(x => x.Snapshot).HasColumnType("jsonb");
        d.Property(x => x.Version).IsRowVersion();
        d.HasIndex(x => new { x.TenantId, x.BranchId, x.Reference });
    }
}
