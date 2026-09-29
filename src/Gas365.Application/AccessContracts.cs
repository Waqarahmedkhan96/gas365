namespace Gas365.Application;

public sealed class TenantScope
{
    public Guid TenantId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public bool IsOwner { get; set; }
    public string[] Permissions { get; set; } = [];
    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);
}

public sealed class AccessException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record RegisterRequest(string Email, string Password, string Name, string BusinessName);
public sealed record LoginRequest(string Email, string Password);
public sealed record EmailRequest(string Email);
public sealed record ConfirmRequest(string Email, string Token);
public sealed record ResetRequest(string Email, string Token, string Password);
public sealed record BranchRequest(string Name, bool Active = true, uint Version = 0);
public sealed record RoleRequest(string Name, string[] Permissions, uint Version = 0);
public sealed record InviteRequest(string Email, string Name, Guid RoleId, Guid[] BranchIds);
public sealed record MemberRequest(Guid RoleId, bool Active, Guid[] BranchIds, uint Version);
// Omitted notification settings keep their current values.
public sealed record TenantRequest(string Name, string TimeZone, int? OverdueCreditDays = null, TimeOnly? ClosingReminder = null);
public sealed record SwitchRequest(Guid MembershipId);
public sealed record SessionGrant(Guid SessionId, Guid UserId, DateTimeOffset ExpiresAt);
public sealed record BranchDto(Guid Id, string Name, bool Active, uint Version);
public sealed record RoleDto(Guid Id, string Name, bool IsOwner, string[] Permissions, uint Version);
public sealed record MemberDto(Guid Id, string Name, string Email, Guid RoleId, bool Active, Guid[] BranchIds, uint Version);
public sealed record WorkspaceDto(Guid MembershipId, Guid TenantId, string Name);
public sealed record MeDto(Guid UserId, string Name, string Email, Guid TenantId, Guid MembershipId, string[] Permissions, bool IsOwner, WorkspaceDto[] Workspaces);
public sealed record TenantDto(Guid TenantId, string Name, string TimeZone, int MaxBranches, int MaxUsers, int MaxAdmins, int OverdueCreditDays, TimeOnly ClosingReminder);
public sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool Current);
public sealed record AuditDto(Guid Id, string Action, Guid? ActorId, Guid? SubjectId, DateTimeOffset OccurredAt);

public interface IAccountMessages
{
    Task SendAsync(string email, string subject, string path, CancellationToken cancellationToken);
}

public interface IIdentityAccess
{
    Task RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<SessionGrant> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<bool> ValidateSessionAsync(Guid sessionId, Guid userId, CancellationToken ct);
    Task ConfirmAsync(ConfirmRequest request, CancellationToken ct);
    Task ForgotAsync(EmailRequest request, CancellationToken ct);
    Task ResendAsync(EmailRequest request, CancellationToken ct);
    Task ResetAsync(ResetRequest request, CancellationToken ct);
    Task<MeDto> MeAsync(CancellationToken ct);
    Task<SessionGrant> SwitchAsync(Guid membershipId, CancellationToken ct);
    Task<SessionDto[]> SessionsAsync(CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
}

public interface IWorkspaceAccess
{
    Task<TenantDto> TenantAsync(CancellationToken ct);
    Task UpdateTenantAsync(TenantRequest request, CancellationToken ct);
    Task<BranchDto[]> BranchesAsync(CancellationToken ct);
    Task<BranchDto> BranchAsync(Guid id, CancellationToken ct);
    Task<BranchDto> CreateBranchAsync(BranchRequest request, CancellationToken ct);
    Task UpdateBranchAsync(Guid id, BranchRequest request, CancellationToken ct);
    Task<RoleDto[]> RolesAsync(CancellationToken ct);
    Task<RoleDto> CreateRoleAsync(RoleRequest request, CancellationToken ct);
    Task UpdateRoleAsync(Guid id, RoleRequest request, CancellationToken ct);
    Task<MemberDto[]> MembersAsync(CancellationToken ct);
    Task InviteAsync(InviteRequest request, CancellationToken ct);
    Task UpdateMemberAsync(Guid id, MemberRequest request, CancellationToken ct);
    Task<AuditDto[]> AuditAsync(CancellationToken ct);
}
