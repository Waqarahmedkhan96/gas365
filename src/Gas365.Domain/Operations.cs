namespace Gas365.Domain;

// A receivable or payable carried over from before Gas365. It ages from the original invoice date the
// operator supplies, but posts on the current business date like every other command.
public sealed class OpeningBalance : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly OriginalDate { get; set; }
    public decimal Amount { get; set; }
}

// Append-only journal for opening balances: Opening, Allocation, AllocationReversal and Reversal. Amount is the
// signed change in the outstanding balance.
public sealed class OpeningBalanceEntry : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OpeningBalanceId { get; set; }
    public long Sequence { get; set; }
    public string Kind { get; set; } = "";
    public decimal Amount { get; set; }
    public Guid? ReversesId { get; set; }
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateOnly BusinessDate { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
}

public sealed class Notification : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string Kind { get; set; } = "";
    public string Permission { get; set; } = "";
    public Guid SubjectId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class NotificationRead : ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class NotificationKinds
{
    public const string LowStock = "LowStock";
    public const string OverdueCredit = "OverdueCredit";
    public const string RegisterOpen = "RegisterOpen";
}

public sealed class ImportBatch : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Status { get; set; } = ImportStatus.Uploaded;
    public string Headers { get; set; } = "[]";
    public string Mapping { get; set; } = "{}";
    public int RowCount { get; set; }
    public int ErrorCount { get; set; }
    public int CommittedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CommittedAt { get; set; }
    public uint Version { get; set; }
}

public sealed class ImportRow : ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid BatchId { get; set; }
    public int RowNumber { get; set; }
    public string Data { get; set; } = "{}";
    public string Errors { get; set; } = "[]";
    public string Status { get; set; } = ImportStatus.Pending;
    public Guid? ResultId { get; set; }
}

public static class ImportStatus
{
    public const string Uploaded = "Uploaded";
    public const string Validated = "Validated";
    public const string Invalid = "Invalid";
    public const string Committed = "Committed";
    public const string PartiallyCommitted = "PartiallyCommitted";
    public const string Committing = "Committing";
    public const string Pending = "Pending";
    public const string Valid = "Valid";
    public const string Failed = "Failed";
    public const int MaxRows = 5_000;
    public const int MaxBytes = 2 * 1024 * 1024;
}

// Health of platform background jobs; not tenant-owned.
public sealed class JobState
{
    public string Name { get; set; } = "";
    public DateTimeOffset? LastStartedAt { get; set; }
    public DateTimeOffset? LastSucceededAt { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }
    public string LastError { get; set; } = "";
    public int ConsecutiveFailures { get; set; }
}

public static class CreditCalculation
{
    // Overdue means strictly more than the configured number of days after the invoice date.
    public static bool Overdue(DateOnly invoiceDate, DateOnly asOf, int overdueDays) => asOf.DayNumber - invoiceDate.DayNumber > overdueDays;
}
