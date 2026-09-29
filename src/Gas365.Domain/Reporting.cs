namespace Gas365.Domain;

// A queued report export. The file lives in PostgreSQL until it expires; only the requesting member can download it.
public sealed class ExportJob : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string ReportKey { get; set; } = "";
    public string Format { get; set; } = "";
    public string Filter { get; set; } = "";
    public string Status { get; set; } = ExportStatus.Queued;
    public int Attempts { get; set; }
    public string Error { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[]? Content { get; set; }
    public int RowCount { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
}

public static class ExportStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Expired = "Expired";
    public const int MaxAttempts = 3;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
}

public static class ReportCalculation
{
    public const string Lpg = "LPG and cylinders";
    public const string SpareParts = "Spare parts";

    // Sale lines sold in SparePart mode are spare-part revenue; every gas and cylinder mode is LPG business.
    public static string SaleCategory(string mode) => mode == "SparePart" ? SpareParts : Lpg;
    public static string ItemCategory(string itemKind) => itemKind == "SparePart" ? SpareParts : Lpg;

    // Groups signed drawer movements into the reconciliation formula lines. Reversals follow their original kind.
    public static string CashCategory(string financeKind)
    {
        var kind = financeKind.EndsWith("Reversal", StringComparison.Ordinal) ? financeKind[..^"Reversal".Length] : financeKind;
        return kind switch
        {
            "SaleCash" => "Sales and collections",
            "CustomerReceipt" => "Customer advances",
            "CylinderCash" => "Cylinder deposits, fees and refunds",
            "PurchaseCash" => "Purchases and purchase costs",
            "SupplierPayment" => "Supplier advances",
            "Expense" => "Expenses",
            "LoanReceived" or "LoanGiven" or "LoanRepayment" or "OwnerFunds" or "OwnerWithdrawal" => "Loans and owner funds",
            "Transfer" or "TransferIn" => "Transfers and bank deposits",
            "CashAdjustmentIn" or "CashAdjustmentOut" => "Adjustments",
            "Opening" => "Opening balance",
            _ => "Other"
        };
    }

    public static readonly string[] CashCategories =
    [
        "Sales and collections", "Customer advances", "Cylinder deposits, fees and refunds", "Purchases and purchase costs",
        "Supplier advances", "Expenses", "Loans and owner funds", "Transfers and bank deposits", "Adjustments"
    ];

    public static decimal? Margin(decimal revenue, decimal profit) => revenue == 0 ? null : Math.Round(profit / revenue * 100, 2, MidpointRounding.AwayFromZero);
}
