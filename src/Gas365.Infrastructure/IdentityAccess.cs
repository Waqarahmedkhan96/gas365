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
        await CreateBusinessAsync(user, business, ct);
        await SendConfirmationAsync(user, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Membership> CreateBusinessAsync(ApplicationUser user, string business, CancellationToken ct)
    {
        // New businesses start a trial of the Business plan with that plan's limits (owner decision, 2026-09-29).
        var plan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == SubscriptionRules.BusinessId, ct);
        var tenant = new Tenant
        {
            Name = business,
            PlanId = plan.Id,
            SubscriptionStatus = SubscriptionRules.Trial,
            TrialEndsAt = DateTimeOffset.UtcNow.AddDays(SubscriptionRules.TrialDays),
            MaxBranches = plan.MaxBranches,
            MaxUsers = plan.MaxUsers,
            MaxAdmins = plan.MaxAdmins
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
        db.SubscriptionEvents.Add(new SubscriptionEvent { TenantId = tenant.TenantId, Kind = "TrialStarted", PlanId = plan.Id, PeriodEnd = tenant.TrialEndsAt, ActorId = user.Id });
        Audit("tenant.created", tenant.TenantId);
        await db.SaveChangesAsync(ct);
        return member;
    }

    private static void Verified(ExternalIdentity identity)
    {
        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.ProviderKey))
            throw new AccessException(401, "Google did not confirm this email address. Sign in with email and password instead.");
    }

    public async Task<SessionGrant?> ExternalLoginAsync(ExternalIdentity identity, CancellationToken ct)
    {
        Verified(identity);
        var email = Email(identity.Email);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(email, ct);
        var user = await users.FindByLoginAsync(identity.Provider, identity.ProviderKey);
        if (user is null)
        {
            user = await users.FindByEmailAsync(email);
            if (user is null) { await transaction.CommitAsync(ct); return null; }
            // The provider verified ownership of this address, so the existing account is linked to it.
            Ensure(await users.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider)));
            if (!user.EmailConfirmed) { user.EmailConfirmed = true; Ensure(await users.UpdateAsync(user)); }
        }
        var membership = await db.Memberships.IgnoreQueryFilters().Where(x => x.UserId == user.Id && x.Active).OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (membership is null || await users.IsLockedOutAsync(user))
            throw new AccessException(401, "Unable to sign in. Ask your business owner to check your access.");
        scope.TenantId = membership.TenantId; scope.UserId = user.Id;
        Audit("login.external", user.Id);
        var grant = await GrantAsync(user, membership, ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    public async Task<SessionGrant> ExternalRegisterAsync(ExternalIdentity identity, ExternalRegisterRequest request, CancellationToken ct)
    {
        Verified(identity);
        var email = Email(identity.Email);
        var name = Text(request.Name);
        var business = Text(request.BusinessName);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockEmailAsync(email, ct);
        if (await users.FindByLoginAsync(identity.Provider, identity.ProviderKey) is not null || await users.FindByEmailAsync(email) is not null)
            throw new AccessException(409, "An account already uses this email. Sign in with Google instead.");
        // No password: the account signs in through the provider until the owner sets one with "Forgot password".
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = name, EmailConfirmed = true };
        Ensure(await users.CreateAsync(user));
        Ensure(await users.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider)));
        var membership = await CreateBusinessAsync(user, business, ct);
        var grant = await GrantAsync(user, membership, ct);
        await transaction.CommitAsync(ct);
        return grant;
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
        if (session.SupportGrantId is { } grantId) await ApplySupportAsync(session.Id, grantId, user, now, ct);
        if (session.LastSeenAt < now.AddMinutes(-5))
            await db.LoginSessions.Where(x => x.Id == session.Id && !x.Revoked).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now), ct);
        return true;
    }

    // A support view replaces the scope with the granted business and least-privilege read permissions. It ends
    // automatically when the grant expires or is revoked, or when the user is no longer a platform administrator.
    private async Task ApplySupportAsync(Guid sessionId, Guid grantId, ApplicationUser user, DateTimeOffset now, CancellationToken ct)
    {
        var grant = await db.SupportGrants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(g => g.Id == grantId && g.RevokedAt == null && g.ExpiresAt > now, ct);
        if (grant is null || !PlatformAdmins.Contains(configuration, user.Email))
        {
            await db.LoginSessions.IgnoreQueryFilters().Where(s => s.Id == sessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SupportGrantId, (Guid?)null), ct);
            return;
        }
        scope.TenantId = grant.TenantId; scope.MembershipId = Guid.Empty; scope.IsOwner = false;
        scope.Permissions = SubscriptionRules.SupportPermissions(grant.IncludeFinance); scope.SupportGrantId = grant.Id;
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
        SupportViewDto? support = null;
        if (scope.SupportGrantId is { } grantId)
        {
            var grant = await db.SupportGrants.IgnoreQueryFilters().AsNoTracking().SingleAsync(g => g.Id == grantId, ct);
            var tenantName = await db.Tenants.IgnoreQueryFilters().Where(t => t.TenantId == grant.TenantId).Select(t => t.Name).SingleAsync(ct);
            support = new(tenantName, grant.ExpiresAt, grant.IncludeFinance);
        }
        return new(user.Id, user.DisplayName, user.Email!, scope.TenantId, scope.MembershipId, scope.Permissions, scope.IsOwner, workspaces,
            user.EmailConfirmed && PlatformAdmins.Contains(configuration, user.Email), support);
    }

    public async Task<SessionGrant> SwitchAsync(Guid membershipId, CancellationToken ct)
    {
        if (scope.Support) throw new AccessException(409, "Exit the support view before switching workspace.");
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
        // In a support view the scope belongs to the viewed business, so the own session is revoked directly.
        if (scope.Support && id == scope.SessionId)
        { await db.LoginSessions.IgnoreQueryFilters().Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(p => p.Revoked, true), ct); return; }
        var session = await db.LoginSessions.SingleOrDefaultAsync(x => x.Id == id && x.MembershipId == scope.MembershipId, ct)
            ?? throw new AccessException(404, "Session not found.");
        session.Revoked = true;
        Audit("session.revoked", id);
        await db.SaveChangesAsync(ct);
    }
}
