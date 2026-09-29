using Gas365.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed partial class Gas365DbContext
{
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();
    public DbSet<OpeningBalanceEntry> OpeningBalanceEntries => Set<OpeningBalanceEntry>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<JobState> JobStates => Set<JobState>();

    private void ConfigureOperations(ModelBuilder model)
    {
        var tenant = model.Entity<Tenant>();
        tenant.Property(x => x.OverdueCreditDays).HasDefaultValue(30).HasSentinel(0);
        tenant.Property(x => x.ClosingReminder).HasDefaultValue(new TimeOnly(21, 0)).HasSentinel(TimeOnly.MinValue);
        tenant.ToTable(t => t.HasCheckConstraint("CK_Tenant_Notifications", "\"OverdueCreditDays\" BETWEEN 1 AND 365"));

        var opening = model.Entity<OpeningBalance>();
        opening.HasKey(x => new { x.TenantId, x.Id }); opening.HasQueryFilter(x => x.TenantId == scope.TenantId);
        opening.HasAlternateKey(x => new { x.TenantId, x.BranchId, x.Id });
        opening.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        opening.HasOne<Customer>().WithMany().HasForeignKey(x => new { x.TenantId, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
        opening.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierId }).OnDelete(DeleteBehavior.Restrict);
        opening.Property(x => x.Reference).HasMaxLength(120); opening.Property(x => x.Amount).HasPrecision(20, 2);
        opening.ToTable(t => t.HasCheckConstraint("CK_OpeningBalance_Party", "(\"CustomerId\" IS NULL) <> (\"SupplierId\" IS NULL) AND \"Amount\" > 0"));

        var entry = model.Entity<OpeningBalanceEntry>();
        entry.HasKey(x => new { x.TenantId, x.Id }); entry.HasQueryFilter(x => x.TenantId == scope.TenantId);
        entry.HasOne<OpeningBalance>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId, x.OpeningBalanceId }).HasPrincipalKey(x => new { x.TenantId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        entry.HasOne<OpeningBalanceEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.ReversesId }).OnDelete(DeleteBehavior.Restrict);
        entry.Property(x => x.Sequence).ValueGeneratedOnAdd();
        entry.HasIndex(x => new { x.TenantId, x.OpeningBalanceId, x.Sequence });
        entry.HasIndex(x => new { x.TenantId, x.BranchId, x.BusinessDate });
        entry.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        entry.HasIndex(x => new { x.TenantId, x.ReversesId }).IsUnique().HasFilter("\"ReversesId\" IS NOT NULL");
        entry.Property(x => x.Amount).HasPrecision(20, 2); entry.Property(x => x.Kind).HasMaxLength(30); entry.Property(x => x.Reason).HasMaxLength(300);
        entry.Property(x => x.IdempotencyKey).HasMaxLength(80); entry.Property(x => x.RequestHash).HasMaxLength(64);
        entry.ToTable(t => t.HasCheckConstraint("CK_OpeningBalanceEntry_Signs",
            "(\"Kind\" = 'Opening' AND \"Amount\" > 0) OR (\"Kind\" IN ('Allocation','Reversal') AND \"Amount\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Amount\" > 0)"));

        var note = model.Entity<Notification>();
        note.HasKey(x => new { x.TenantId, x.Id }); note.HasQueryFilter(x => x.TenantId == scope.TenantId);
        note.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        // One active alert per condition; a resolved alert may recur later as a new row.
        note.HasIndex(x => new { x.TenantId, x.Key }).IsUnique().HasFilter("\"ResolvedAt\" IS NULL");
        note.HasIndex(x => new { x.TenantId, x.ResolvedAt, x.CreatedAt });
        note.Property(x => x.Kind).HasMaxLength(30); note.Property(x => x.Permission).HasMaxLength(60); note.Property(x => x.Key).HasMaxLength(120);
        note.Property(x => x.Title).HasMaxLength(160); note.Property(x => x.Body).HasMaxLength(400);

        var read = model.Entity<NotificationRead>();
        read.HasKey(x => new { x.TenantId, x.NotificationId, x.UserId }); read.HasQueryFilter(x => x.TenantId == scope.TenantId);
        read.HasOne<Notification>().WithMany().HasForeignKey(x => new { x.TenantId, x.NotificationId }).OnDelete(DeleteBehavior.Cascade);

        var batch = model.Entity<ImportBatch>();
        batch.HasKey(x => new { x.TenantId, x.Id }); batch.HasQueryFilter(x => x.TenantId == scope.TenantId);
        batch.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.TenantId, x.MembershipId }).OnDelete(DeleteBehavior.Restrict);
        batch.HasIndex(x => new { x.TenantId, x.CreatedAt });
        batch.Property(x => x.Type).HasMaxLength(40); batch.Property(x => x.FileName).HasMaxLength(160); batch.Property(x => x.Status).HasMaxLength(30);
        batch.Property(x => x.Version).IsRowVersion();

        var row = model.Entity<ImportRow>();
        row.HasKey(x => new { x.TenantId, x.BatchId, x.RowNumber }); row.HasQueryFilter(x => x.TenantId == scope.TenantId);
        row.HasOne<ImportBatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BatchId }).OnDelete(DeleteBehavior.Cascade);
        row.Property(x => x.Status).HasMaxLength(20);

        var job = model.Entity<JobState>();
        job.HasKey(x => x.Name); job.Property(x => x.Name).HasMaxLength(60); job.Property(x => x.LastError).HasMaxLength(300);
    }
}
