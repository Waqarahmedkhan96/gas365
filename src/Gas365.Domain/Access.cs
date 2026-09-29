namespace Gas365.Domain;

public static class Permissions
{
    public const string TenantManage = "tenant.manage";
    public const string BranchManage = "branches.manage";
    public const string TeamManage = "users.manage";
    public const string RoleManage = "roles.manage";
    public const string AuditView = "audit.view";
    public const string CatalogManage = "catalog.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryPost = "inventory.post";
    public const string InventoryReverse = "inventory.reverse";
    public const string PurchaseView = "purchases.view_cost";
    public const string PurchaseDraft = "purchases.create";
    public const string PurchasePost = "purchases.post";
    public const string PurchasePay = "purchases.pay";
    public const string PurchaseReverse = "purchases.reverse";
    public const string SupplierManage = "suppliers.manage";
    public const string CustomerView = "customers.view";
    public const string CustomerManage = "customers.manage";
    public const string PriceManage = "prices.manage";
    public const string SalesView = "sales.view";
    public const string SalesDraft = "sales.create";
    public const string SalesDiscount = "sales.discount";
    public const string SalesPost = "sales.post";
    public const string SalesPay = "sales.pay";
    public const string SalesReverse = "sales.reverse";
    public const string SalesProfit = "sales.view_profit";
    public const string CylinderManage = "cylinders.configure";
    public const string CylinderWaive = "cylinders.waive_deposit";
    public const string CylinderReturn = "cylinders.return";
    public const string CylinderApprove = "cylinders.approve_damage_loss";
    public const string FinanceView = "finance.view";
    public const string FinancePost = "finance.post";
    public const string FinanceManage = "finance.manage";
    public const string FinanceReverse = "finance.reverse";
    public const string RegisterClose = "register.close";
    public const string RegisterReopen = "register.reopen";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public static readonly string[] All = [ReportsView, ReportsExport, FinanceView, FinancePost, FinanceManage, FinanceReverse, RegisterClose, RegisterReopen, TenantManage, BranchManage, TeamManage, RoleManage, AuditView, CatalogManage, InventoryView, InventoryPost, InventoryReverse, PurchaseView, PurchaseDraft, PurchasePost, PurchasePay, PurchaseReverse, SupplierManage, CustomerView, CustomerManage, PriceManage, SalesView, SalesDraft, SalesDiscount, SalesPost, SalesPay, SalesReverse, SalesProfit, CylinderManage, CylinderWaive, CylinderReturn, CylinderApprove];
}

public interface ITenantOwned { Guid TenantId { get; set; } }

public sealed class Tenant : ITenantOwned
{
    public Guid TenantId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string TimeZone { get; set; } = "Asia/Karachi";
    public int MaxBranches { get; set; } = 3;
    public int MaxUsers { get; set; } = 10;
    public int MaxAdmins { get; set; } = 3;
}

public sealed class Branch : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}

public sealed class TenantRole : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool IsOwner { get; set; }
    public string[] Permissions { get; set; } = [];
    public uint Version { get; set; }
}

public sealed class Membership : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}

public sealed class BranchAccess : ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid BranchId { get; set; }
}

public sealed class LoginSession : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public bool Revoked { get; set; }
    public string SecurityStamp { get; set; } = "";
}

public sealed class AuditEvent : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public Guid? SubjectId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
