namespace Gas365.Domain;

public static class Permissions
{
    public const string TenantManage = "tenant.manage";
    public const string BranchManage = "branches.manage";
    public const string TeamManage = "users.manage";
    public const string RoleManage = "roles.manage";
    public const string AuditView = "audit.view";
    public static readonly string[] All = [TenantManage, BranchManage, TeamManage, RoleManage, AuditView];
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
