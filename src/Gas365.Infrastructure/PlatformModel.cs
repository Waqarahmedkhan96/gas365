using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<SupportGrant> SupportGrants => Set<SupportGrant>();

    private void ConfigurePlatform(ModelBuilder model)
    {
        var plan = model.Entity<Plan>();
        plan.HasKey(x => x.Id); plan.HasIndex(x => x.Code).IsUnique();
        plan.Property(x => x.Code).HasMaxLength(30); plan.Property(x => x.Name).HasMaxLength(60); plan.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
        plan.Property(x => x.Version).IsRowVersion();
        plan.ToTable(t => t.HasCheckConstraint("CK_Plan_Limits", "\"MaxBranches\" > 0 AND \"MaxUsers\" > 0 AND \"MaxAdmins\" > 0 AND \"MaxAdmins\" <= \"MaxUsers\" AND (\"MonthlyPrice\" IS NULL OR \"MonthlyPrice\" >= 0)"));

        var tenant = model.Entity<Tenant>();
        tenant.Property(x => x.SubscriptionStatus).HasMaxLength(20);
        tenant.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        tenant.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        tenant.HasOne<Plan>().WithMany().HasForeignKey(x => x.RequestedPlanId).OnDelete(DeleteBehavior.Restrict);
        tenant.ToTable(t => t.HasCheckConstraint("CK_Tenant_Subscription", "\"SubscriptionStatus\" IN ('Trial','Active','Expired') AND (\"SubscriptionStatus\" <> 'Trial' OR \"TrialEndsAt\" IS NOT NULL)"));

        var history = model.Entity<SubscriptionEvent>();
        history.HasKey(x => new { x.TenantId, x.Id }); history.HasQueryFilter(x => x.TenantId == scope.TenantId);
        history.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        history.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        history.HasIndex(x => new { x.TenantId, x.OccurredAt });
        history.Property(x => x.Kind).HasMaxLength(30); history.Property(x => x.Amount).HasPrecision(18, 2);
        history.Property(x => x.Reference).HasMaxLength(120); history.Property(x => x.Note).HasMaxLength(300);

        var grant = model.Entity<SupportGrant>();
        grant.HasKey(x => new { x.TenantId, x.Id }); grant.HasQueryFilter(x => x.TenantId == scope.TenantId);
        grant.HasIndex(x => x.Id).IsUnique();
        grant.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        grant.HasIndex(x => new { x.TenantId, x.ExpiresAt });
        grant.Property(x => x.Reason).HasMaxLength(300);
        grant.ToTable(t => t.HasCheckConstraint("CK_SupportGrant_Window", "\"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" <= \"CreatedAt\" + interval '72 hours'"));
    }
}
