using Gas365.Application;
using Gas365.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext(DbContextOptions<Gas365DbContext> options, TenantScope scope)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public Gas365DbContext(DbContextOptions<Gas365DbContext> options) : this(options, new TenantScope()) { }
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<BranchAccess> BranchAccess => Set<BranchAccess>();
    public DbSet<LoginSession> LoginSessions => Set<LoginSession>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        ConfigureInventory(model);
        ConfigurePurchasing(model);
        ConfigureCustomers(model);
        ConfigureSales(model);
        ConfigurePostedSales(model);
        ConfigureCylinderLoans(model);
        ConfigureFinance(model);
        ConfigureReporting(model);
        model.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(120);
        model.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        model.Entity<Tenant>().HasKey(x => x.TenantId);
        model.Entity<Tenant>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<Tenant>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<Tenant>().Property(x => x.TimeZone).HasMaxLength(100);
        model.Entity<Tenant>().ToTable(t => t.HasCheckConstraint("CK_Tenant_Limits", "\"MaxBranches\" > 0 AND \"MaxUsers\" > 0 AND \"MaxAdmins\" > 0 AND \"MaxAdmins\" <= \"MaxUsers\""));
        model.Entity<Branch>().HasKey(x => new { x.TenantId, x.Id });
        model.Entity<Branch>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<Branch>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Branch>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        model.Entity<Branch>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<Branch>().Property(x => x.Version).IsRowVersion();
        model.Entity<TenantRole>().HasKey(x => new { x.TenantId, x.Id });
        model.Entity<TenantRole>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<TenantRole>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<TenantRole>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        model.Entity<TenantRole>().HasIndex(x => x.TenantId).IsUnique().HasFilter("\"IsOwner\" = TRUE");
        model.Entity<TenantRole>().Property(x => x.Name).HasMaxLength(80);
        model.Entity<TenantRole>().Property(x => x.Version).IsRowVersion();
        model.Entity<Membership>().HasKey(x => new { x.TenantId, x.Id });
        model.Entity<Membership>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<Membership>().HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        model.Entity<Membership>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Membership>().HasOne<TenantRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.RoleId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Membership>().Property(x => x.Version).IsRowVersion();
        model.Entity<BranchAccess>().HasKey(x => new { x.TenantId, x.MembershipId, x.BranchId });
        model.Entity<BranchAccess>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<BranchAccess>().HasOne<Membership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).OnDelete(DeleteBehavior.Cascade);
        model.Entity<BranchAccess>().HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LoginSession>().HasKey(x => new { x.TenantId, x.Id });
        model.Entity<LoginSession>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<LoginSession>().HasIndex(x => x.Id).IsUnique();
        model.Entity<LoginSession>().HasOne<Membership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).OnDelete(DeleteBehavior.Cascade);
        model.Entity<LoginSession>().Property(x => x.SecurityStamp).HasMaxLength(100);
        model.Entity<AuditEvent>().HasKey(x => new { x.TenantId, x.Id });
        model.Entity<AuditEvent>().HasQueryFilter(x => x.TenantId == scope.TenantId);
        model.Entity<AuditEvent>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AuditEvent>().Property(x => x.Action).HasMaxLength(80);
        model.Entity<AuditEvent>().HasIndex(x => new { x.TenantId, x.OccurredAt });
    }

    private void ValidateWrites()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (scope.TenantId == Guid.Empty || entry.Entity.TenantId != scope.TenantId ||
                (entry.State != EntityState.Added && entry.Property(nameof(ITenantOwned.TenantId)).OriginalValue is Guid original && original != scope.TenantId))
                throw new InvalidOperationException("Tenant boundary violation.");
            if (entry.Entity is (AuditEvent or StockDocument or StockMovement or PurchaseDocument or PurchaseEntry or SaleDocument or SaleEntry or CylinderLoan or CylinderLoanEntry or FinanceEntry or RegisterEvent) && entry.State != EntityState.Added)
                throw new InvalidOperationException("Posted and audit records are append-only.");
            // Finance master data may be renamed or retired; database guards keep identity fields fixed.
            if (entry.Entity is (MoneyAccount or ExpenseCategory) && entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Finance master records cannot be deleted.");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { ValidateWrites(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { ValidateWrites(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
