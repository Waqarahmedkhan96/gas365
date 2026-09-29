using Gas365.Application;
using Gas365.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gas365.Infrastructure;

public sealed class WorkspaceAccess(Gas365DbContext db, TenantScope scope, UserManager<ApplicationUser> users,
    IAccountMessages messages) : IWorkspaceAccess
{
    private void Require(string permission)
    { if (!scope.Has(permission)) throw new AccessException(403, "You do not have permission for this action."); }
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new AuditEvent
    { TenantId = scope.TenantId, ActorId = scope.UserId, Action = action, SubjectId = id });
    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT 1 FROM \"Tenants\" WHERE \"TenantId\" = {scope.TenantId} FOR UPDATE", ct);
    private static BranchDto Dto(Branch x) => new(x.Id, x.Name, x.Active, x.Version);
    private static RoleDto Dto(TenantRole x) => new(x.Id, x.Name, x.IsOwner, x.Permissions, x.Version);

    public async Task<TenantDto> TenantAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(ct);
        return new(tenant.TenantId, tenant.Name, tenant.TimeZone, tenant.MaxBranches, tenant.MaxUsers, tenant.MaxAdmins, tenant.OverdueCreditDays, tenant.ClosingReminder);
    }

    public async Task UpdateTenantAsync(TenantRequest request, CancellationToken ct)
    {
        Require(Permissions.TenantManage);
        var name = IdentityAccess.Text(request.Name);
        var zone = IdentityAccess.Text(request.TimeZone, 100);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _) || !zone.Contains('/'))
            throw new AccessException(400, "Choose an IANA time zone, such as Asia/Karachi.");
        if (request.OverdueCreditDays is < 1 or > 365) throw new AccessException(400, "Overdue credit days must be between 1 and 365.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var tenant = await db.Tenants.SingleAsync(ct);
        tenant.Name = name; tenant.TimeZone = zone;
        tenant.OverdueCreditDays = request.OverdueCreditDays ?? tenant.OverdueCreditDays; tenant.ClosingReminder = request.ClosingReminder ?? tenant.ClosingReminder;
        Audit("tenant.updated", tenant.TenantId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private IQueryable<Branch> VisibleBranches() => db.Branches.Where(x => scope.IsOwner || scope.Has(Permissions.BranchManage) ||
        (x.Active && db.BranchAccess.Any(a => a.MembershipId == scope.MembershipId && a.BranchId == x.Id)));

    public async Task<BranchDto[]> BranchesAsync(CancellationToken ct) =>
        (await VisibleBranches().AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct)).Select(Dto).ToArray();

    public async Task<BranchDto> BranchAsync(Guid id, CancellationToken ct) => Dto(
        await VisibleBranches().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Branch not found."));

    public async Task<BranchDto> CreateBranchAsync(BranchRequest request, CancellationToken ct)
    {
        Require(Permissions.BranchManage);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var tenant = await db.Tenants.SingleAsync(ct);
        if (await db.Branches.CountAsync(x => x.Active, ct) >= tenant.MaxBranches) throw new AccessException(409, "The active branch limit has been reached.");
        var branch = new Branch { TenantId = scope.TenantId, Name = IdentityAccess.Text(request.Name) };
        db.Branches.Add(branch); Audit("branch.created", branch.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Dto(branch);
    }

    public async Task UpdateBranchAsync(Guid id, BranchRequest request, CancellationToken ct)
    {
        Require(Permissions.BranchManage);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var branch = await db.Branches.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Branch not found.");
        if (branch.Version != request.Version) throw new AccessException(409, "This branch changed. Refresh and try again.");
        if (!branch.Active && request.Active && await db.Branches.CountAsync(x => x.Active, ct) >= (await db.Tenants.SingleAsync(ct)).MaxBranches)
            throw new AccessException(409, "The active branch limit has been reached.");
        if (branch.Active && !request.Active && await db.Branches.CountAsync(x => x.Active, ct) == 1)
            throw new AccessException(409, "Keep at least one active branch.");
        branch.Name = IdentityAccess.Text(request.Name); branch.Active = request.Active;
        Audit("branch.updated", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public async Task<RoleDto[]> RolesAsync(CancellationToken ct)
    {
        Require(Permissions.TeamManage);
        return (await db.TenantRoles.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(ct)).Select(Dto).ToArray();
    }

    private string[] ValidatePermissions(string[] permissions)
    {
        Require(Permissions.RoleManage);
        if (!scope.IsOwner) throw new AccessException(403, "Only the owner can define roles.");
        if (permissions is null || permissions.Any(x => !Permissions.All.Contains(x))) throw new AccessException(400, "Unknown permission.");
        return permissions.Distinct().Order().ToArray();
    }

    public async Task<RoleDto> CreateRoleAsync(RoleRequest request, CancellationToken ct)
    {
        var permissions = ValidatePermissions(request.Permissions);
        var role = new TenantRole { TenantId = scope.TenantId, Name = IdentityAccess.Text(request.Name, 80), Permissions = permissions };
        db.TenantRoles.Add(role); Audit("role.created", role.Id);
        await db.SaveChangesAsync(ct);
        return Dto(role);
    }

    public async Task UpdateRoleAsync(Guid id, RoleRequest request, CancellationToken ct)
    {
        var permissions = ValidatePermissions(request.Permissions);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var role = await db.TenantRoles.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Role not found.");
        if (role.IsOwner) throw new AccessException(409, "The owner role cannot be changed.");
        if (role.Version != request.Version) throw new AccessException(409, "This role changed. Refresh and try again.");
        role.Name = IdentityAccess.Text(request.Name, 80); role.Permissions = permissions;
        await CheckLimitsAsync(null, null, ct);
        Audit("role.updated", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public async Task<MemberDto[]> MembersAsync(CancellationToken ct)
    {
        Require(Permissions.TeamManage);
        var rows = await (from member in db.Memberships.AsNoTracking()
                          join user in db.Users on member.UserId equals user.Id
                          orderby user.DisplayName
                          select new { member, user.DisplayName, user.Email }).ToArrayAsync(ct);
        var grants = await db.BranchAccess.AsNoTracking().ToArrayAsync(ct);
        return rows.Select(x => new MemberDto(x.member.Id, x.DisplayName, x.Email!, x.member.RoleId, x.member.Active,
            grants.Where(a => a.MembershipId == x.member.Id).Select(a => a.BranchId).ToArray(), x.member.Version)).ToArray();
    }

    private async Task<TenantRole> ValidateRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await db.TenantRoles.SingleOrDefaultAsync(x => x.Id == roleId, ct) ?? throw new AccessException(404, "Role not found.");
        if (role.IsOwner || role.Permissions.Any(x => !scope.Has(x))) throw new AccessException(403, "You cannot assign this role.");
        return role;
    }

    private async Task<Guid[]> ValidateBranchesAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids is null) throw new AccessException(400, "Branch access is required.");
        var distinct = ids.Distinct().ToArray();
        if (await db.Branches.CountAsync(x => distinct.Contains(x.Id) && x.Active, ct) != distinct.Length)
            throw new AccessException(404, "A selected branch was not found.");
        // A team administrator may only delegate branches they can see.
        if (await VisibleBranches().CountAsync(x => distinct.Contains(x.Id), ct) != distinct.Length)
            throw new AccessException(403, "You cannot delegate access to that branch.");
        return distinct;
    }

    private static bool Admin(TenantRole role) => role.IsOwner || role.Permissions.Any(x =>
        x is Permissions.TeamManage or Permissions.RoleManage or Permissions.TenantManage or Permissions.BranchManage);

    private async Task CheckLimitsAsync(Membership? changed, Membership? added, CancellationToken ct)
    {
        var tenant = await db.Tenants.SingleAsync(ct);
        var roles = await db.TenantRoles.ToDictionaryAsync(x => x.Id, ct);
        // Tracking returns changed role/member values when checking role escalation and reactivation.
        var members = await db.Memberships.ToListAsync(ct);
        if (added is not null) members.Add(added);
        var active = members.Where(x => x.Active).ToArray();
        if (active.Length > tenant.MaxUsers || active.Count(x => Admin(roles[x.RoleId])) > tenant.MaxAdmins)
            throw new AccessException(409, "The active user or administrator limit has been reached.");
    }

    public async Task InviteAsync(InviteRequest request, CancellationToken ct)
    {
        Require(Permissions.TeamManage);
        var email = IdentityAccess.Email(request.Email);
        var name = IdentityAccess.Text(request.Name);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        await ValidateRoleAsync(request.RoleId, ct);
        var branches = await ValidateBranchesAsync(request.BranchIds, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({email.ToUpperInvariant()}))", ct);
        var user = await users.FindByEmailAsync(email);
        var created = user is null;
        if (created)
        {
            user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, DisplayName = name };
            IdentityAccess.Ensure(await users.CreateAsync(user));
        }
        if (await db.Memberships.AnyAsync(x => x.UserId == user!.Id, ct)) throw new AccessException(409, "This account is already in the team.");
        var member = new Membership { TenantId = scope.TenantId, UserId = user!.Id, RoleId = request.RoleId };
        await CheckLimitsAsync(null, member, ct);
        db.Memberships.Add(member);
        db.BranchAccess.AddRange(branches.Select(id => new BranchAccess { TenantId = scope.TenantId, MembershipId = member.Id, BranchId = id }));
        Audit("member.invited", member.Id);
        await db.SaveChangesAsync(ct);
        if (created)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await messages.SendAsync(email, "Join your Gas365 team", $"/reset?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}", ct);
        }
        else await messages.SendAsync(email, "A Gas365 workspace is available", "/login", ct);
        await transaction.CommitAsync(ct);
    }

    public async Task UpdateMemberAsync(Guid id, MemberRequest request, CancellationToken ct)
    {
        Require(Permissions.TeamManage);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AccessException(404, "Team member not found.");
        var previousRole = await db.TenantRoles.SingleAsync(x => x.Id == member.RoleId, ct);
        if (member.Id == scope.MembershipId || previousRole.IsOwner || previousRole.Permissions.Any(x => !scope.Has(x)))
            throw new AccessException(403, "You cannot change this member.");
        if (member.Version != request.Version) throw new AccessException(409, "This member changed. Refresh and try again.");
        await ValidateRoleAsync(request.RoleId, ct);
        var branches = await ValidateBranchesAsync(request.BranchIds, ct);
        member.RoleId = request.RoleId; member.Active = request.Active;
        await CheckLimitsAsync(member, null, ct);
        db.BranchAccess.RemoveRange(await db.BranchAccess.Where(x => x.MembershipId == id).ToArrayAsync(ct));
        // Persist removals before replacing a composite-key grant in the same unit of work.
        await db.SaveChangesAsync(ct);
        db.BranchAccess.AddRange(branches.Select(branchId => new BranchAccess { TenantId = scope.TenantId, MembershipId = id, BranchId = branchId }));
        await db.LoginSessions.Where(x => x.MembershipId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
        Audit("member.updated", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public Task<AuditDto[]> AuditAsync(CancellationToken ct)
    {
        Require(Permissions.AuditView);
        return db.AuditEvents.AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(100)
            .Select(x => new AuditDto(x.Id, x.Action, x.ActorId, x.SubjectId, x.OccurredAt)).ToArrayAsync(ct);
    }
}
