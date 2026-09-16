using Gas365.Application;
using Gas365.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Gas365.Infrastructure;

// Unfiltered reads in this service establish identity before tenant context exists.
// Every such read is bound to a verified user, protected session, or validated membership.
public sealed class IdentityAccess(Gas365DbContext db, TenantScope scope, UserManager<ApplicationUser> users,
    IAccountMessages messages, IConfiguration configuration) : IIdentityAccess
{
    internal static string Text(string? value, int max = 120)
    {
        var result = value?.Trim() ?? "";
        if (result.Length == 0 || result.Length > max) throw new AccessException(400, $"Enter a value between 1 and {max} characters.");
        return result;
    }

    internal static string Email(string? value)
    {
        var email = Text(value, 254);
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new AccessException(400, "Enter a valid email address.");
        return email;
    }

    private static void Password(string? password)
    { if (string.IsNullOrEmpty(password) || password.Length > 128) throw new AccessException(400, "Password must contain at most 128 characters."); }

    internal static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new AccessException(400, "Unable to update account. Use a unique email and a password of at least 12 characters with upper/lowercase letters, a number and a symbol.");
    }

    internal void Audit(string action, Guid? subject = null) => db.AuditEvents.Add(new AuditEvent
    { TenantId = scope.TenantId, ActorId = scope.UserId == Guid.Empty ? null : scope.UserId, Action = action, SubjectId = subject });

    private async Task LockEmailAsync(string email, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({email.ToUpperInvariant()}))", ct);

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        Password(request.Password);
        var email = Email(request.Email);
        var name = Text(request.Name);
        var business = Text(request.BusinessName);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(email, ct);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = name };
        Ensure(await users.CreateAsync(user, request.Password));
        var tenant = new Tenant
        {
            Name = business,
            MaxBranches = configuration.GetValue("Provisioning:MaxBranches", 3),
            MaxUsers = configuration.GetValue("Provisioning:MaxUsers", 10),
            MaxAdmins = configuration.GetValue("Provisioning:MaxAdmins", 3)
        };
        scope.TenantId = tenant.TenantId;
        scope.UserId = user.Id;
        var owner = new TenantRole { TenantId = tenant.TenantId, Name = "Owner", IsOwner = true, Permissions = Permissions.All };
        var admin = new TenantRole { TenantId = tenant.TenantId, Name = "Admin", Permissions = [Permissions.BranchManage, Permissions.TeamManage, Permissions.AuditView] };
        var worker = new TenantRole { TenantId = tenant.TenantId, Name = "Worker" };
        var auditor = new TenantRole { TenantId = tenant.TenantId, Name = "Auditor", Permissions = [Permissions.AuditView] };
        var member = new Membership { TenantId = tenant.TenantId, UserId = user.Id, RoleId = owner.Id };
        db.Tenants.Add(tenant);
        db.TenantRoles.AddRange(owner, admin, worker, auditor);
        db.Memberships.Add(member);
        db.Branches.Add(new Branch { TenantId = tenant.TenantId, Name = "Main branch" });
        Audit("tenant.created", tenant.TenantId);
        await db.SaveChangesAsync(ct);
        await SendConfirmationAsync(user, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task SendConfirmationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await messages.SendAsync(user.Email!, "Verify your Gas365 account",
            $"/confirm?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}", ct);
    }

    public async Task<SessionGrant> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        Password(request.Password);
        var email = Email(request.Email);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(email, ct);
        var user = await users.FindByEmailAsync(email);
        if (user is null) throw new AccessException(401, "Unable to sign in. Check your credentials and verify your email.");
        if (await users.IsLockedOutAsync(user)) throw new AccessException(401, "Unable to sign in. Check your credentials and verify your email.");
        var membership = await db.Memberships.IgnoreQueryFilters().Where(x => x.UserId == user.Id && x.Active).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (membership is not null) { scope.TenantId = membership.TenantId; scope.UserId = user.Id; }
        if (!await users.CheckPasswordAsync(user, request.Password) || !user.EmailConfirmed || membership is null)
        {
            Ensure(await users.AccessFailedAsync(user));
            if (membership is not null) { Audit("login.failed", user.Id); await db.SaveChangesAsync(ct); }
            await transaction.CommitAsync(ct);
            throw new AccessException(401, "Unable to sign in. Check your credentials and verify your email.");
        }
        Ensure(await users.ResetAccessFailedCountAsync(user));
        var grant = await GrantAsync(user, membership, ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    private async Task<SessionGrant> GrantAsync(ApplicationUser user, Membership membership, CancellationToken ct)
    {
        scope.TenantId = membership.TenantId;
        scope.UserId = user.Id;
        scope.MembershipId = membership.Id;
        var now = DateTimeOffset.UtcNow;
        var session = new LoginSession
        {
            TenantId = membership.TenantId,
            MembershipId = membership.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddHours(8),
            SecurityStamp = user.SecurityStamp!
        };
        db.LoginSessions.Add(session);
        Audit("session.created", session.Id);
        await db.SaveChangesAsync(ct);
        return new(session.Id, user.Id, session.ExpiresAt);
    }

    public async Task<bool> ValidateSessionAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var session = await db.LoginSessions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId && !x.Revoked, ct);
        if (session is null || session.ExpiresAt <= now || session.LastSeenAt <= now.AddMinutes(-30)) return false;
        var member = await db.Memberships.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == session.TenantId && x.Id == session.MembershipId && x.UserId == userId && x.Active, ct);
        if (member is null) return false;
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed || user.SecurityStamp != session.SecurityStamp || await users.IsLockedOutAsync(user)) return false;
        var role = await db.TenantRoles.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.TenantId == member.TenantId && x.Id == member.RoleId, ct);
        scope.TenantId = member.TenantId;
        scope.MembershipId = member.Id;
        scope.UserId = userId;
        scope.SessionId = session.Id;
        scope.IsOwner = role.IsOwner;
        scope.Permissions = role.Permissions;
        if (session.LastSeenAt < now.AddMinutes(-5))
            await db.LoginSessions.Where(x => x.Id == session.Id && !x.Revoked).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now), ct);
        return true;
    }

    public async Task ConfirmAsync(ConfirmRequest request, CancellationToken ct)
    {
        var email = Email(request.Email);
        var token = Text(request.Token, 4096);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(email, ct);
        var user = await users.FindByEmailAsync(email);
        if (user is null || !(await users.ConfirmEmailAsync(user, token)).Succeeded)
            throw new AccessException(400, "The verification link is invalid or expired.");
        foreach (var member in await db.Memberships.IgnoreQueryFilters().AsNoTracking().Where(x => x.UserId == user.Id).ToListAsync(ct))
        {
            scope.TenantId = member.TenantId; scope.UserId = user.Id;
            Audit("email.verified", user.Id);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task ResendAsync(EmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(Email(request.Email));
        if (user is not null && !user.EmailConfirmed) await SendConfirmationAsync(user, ct);
    }

    public async Task ForgotAsync(EmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(Email(request.Email));
        if (user is not null && user.EmailConfirmed)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await messages.SendAsync(user.Email!, "Reset your Gas365 password",
                $"/reset?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}", ct);
        }
    }

    public async Task ResetAsync(ResetRequest request, CancellationToken ct)
    {
        Password(request.Password);
        var token = Text(request.Token, 4096);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(Email(request.Email), ct);
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) throw new AccessException(400, "The reset link is invalid or expired.");
        Ensure(await users.ResetPasswordAsync(user, token, request.Password));
        // A successful reset consumes the security-stamp-bound token and revokes all workspaces.
        user.EmailConfirmed = true;
        Ensure(await users.UpdateAsync(user));
        var memberships = db.Memberships.IgnoreQueryFilters().Where(x => x.UserId == user.Id);
        await db.LoginSessions.IgnoreQueryFilters().Where(s => memberships.Any(m => m.TenantId == s.TenantId && m.Id == s.MembershipId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
        foreach (var member in await memberships.AsNoTracking().ToListAsync(ct))
        {
            scope.TenantId = member.TenantId; scope.UserId = user.Id;
            Audit("password.reset", user.Id);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task<MeDto> MeAsync(CancellationToken ct)
    {
        var user = await users.FindByIdAsync(scope.UserId.ToString()) ?? throw new AccessException(401, "Sign in again.");
        var workspaces = await (from member in db.Memberships.IgnoreQueryFilters().AsNoTracking()
                                join tenant in db.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.TenantId
                                where member.UserId == scope.UserId && member.Active
                                select new WorkspaceDto(member.Id, tenant.TenantId, tenant.Name)).ToArrayAsync(ct);
        return new(user.Id, user.DisplayName, user.Email!, scope.TenantId, scope.MembershipId, scope.Permissions, scope.IsOwner, workspaces);
    }

    public async Task<SessionGrant> SwitchAsync(Guid membershipId, CancellationToken ct)
    {
        var member = await db.Memberships.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == membershipId && x.UserId == scope.UserId && x.Active, ct)
            ?? throw new AccessException(404, "Workspace not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await RevokeAsync(scope.SessionId, ct);
        var user = await users.FindByIdAsync(scope.UserId.ToString()) ?? throw new AccessException(401, "Sign in again.");
        var grant = await GrantAsync(user, member, ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    public Task<SessionDto[]> SessionsAsync(CancellationToken ct) => db.LoginSessions.AsNoTracking()
        .Where(x => x.MembershipId == scope.MembershipId && !x.Revoked && x.ExpiresAt > DateTimeOffset.UtcNow && x.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-30))
        .OrderByDescending(x => x.CreatedAt).Select(x => new SessionDto(x.Id, x.CreatedAt, x.ExpiresAt, x.Id == scope.SessionId)).ToArrayAsync(ct);

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var session = await db.LoginSessions.SingleOrDefaultAsync(x => x.Id == id && x.MembershipId == scope.MembershipId, ct)
            ?? throw new AccessException(404, "Session not found.");
        session.Revoked = true;
        Audit("session.revoked", id);
        await db.SaveChangesAsync(ct);
    }
}
