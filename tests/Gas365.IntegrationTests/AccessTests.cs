using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gas365.IntegrationTests;

public sealed class TestMessages : IAccountMessages
{
    public ConcurrentDictionary<string, string> Links { get; } = new();
    public Task SendAsync(string email, string subject, string path, CancellationToken ct)
    { Links[email] = path; return Task.CompletedTask; }
    public string Token(string email) => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("https://local" + Links[email]).Query)["token"].ToString();
}

public sealed class AccessTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string Password = "Synthetic-Only-2026!";
    private readonly TestMessages messages = new();
    private WebApplicationFactory<Program> application = null!;

    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = database.ConnectionString }));
            builder.ConfigureServices(services => { services.RemoveAll<IAccountMessages>(); services.AddSingleton<IAccountMessages>(messages); });
        });
    }
    public async Task DisposeAsync() => await application.DisposeAsync();
    private HttpClient Client() => application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, HttpMethod method, object? body = null, Guid? tenant = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (tenant.HasValue) request.Headers.Add("X-Workspace", tenant.ToString());
        if (method != HttpMethod.Get)
        {
            var csrf = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/auth/csrf");
            request.Headers.Add("X-CSRF-TOKEN", csrf!["token"]);
            request.Content = JsonContent.Create(body ?? new { });
        }
        return await client.SendAsync(request);
    }

    private async Task<(HttpClient Client, MeDto Me, string Email)> Owner()
    {
        var client = Client();
        var email = $"owner-{Guid.NewGuid():N}@test.example";
        (await Send(client, "/api/auth/register", HttpMethod.Post, new RegisterRequest(email, Password, "Test owner", "Business " + Guid.NewGuid()))).EnsureSuccessStatusCode();
        (await Send(client, "/api/auth/confirm", HttpMethod.Post, new ConfirmRequest(email, messages.Token(email)))).EnsureSuccessStatusCode();
        (await Send(client, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        return (client, (await client.GetFromJsonAsync<MeDto>("/api/auth/me"))!, email);
    }
    private static async Task<T> Get<T>(HttpClient client, string path, Guid tenant) =>
        (await (await Send(client, "/api/workspace/" + path, HttpMethod.Get, tenant: tenant)).Content.ReadFromJsonAsync<T>())!;

    [Fact]
    public async Task VerificationCsrfCookieAndLogoutAreEnforced()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var email = $"verify-{Guid.NewGuid():N}@test.example";
        var registration = new RegisterRequest(email, Password, "Synthetic", "Synthetic tenant");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        (await Send(client, "/api/auth/register", HttpMethod.Post, registration)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, "/api/auth/confirm", HttpMethod.Post, new ConfirmRequest(email, "invalid"))).StatusCode);
        (await Send(client, "/api/auth/confirm", HttpMethod.Post, new ConfirmRequest(email, messages.Token(email)))).EnsureSuccessStatusCode();
        var login = await Send(client, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password));
        login.EnsureSuccessStatusCode();
        var cookie = string.Join(';', login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        (await Send(client, "/api/auth/logout", HttpMethod.Post)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("tenant", "GET")]
    [InlineData("tenant", "PUT")]
    [InlineData("branches", "GET")]
    [InlineData("branches", "POST")]
    [InlineData("roles", "GET")]
    [InlineData("roles", "POST")]
    [InlineData("members", "GET")]
    [InlineData("members", "POST")]
    [InlineData("audit", "GET")]
    public async Task EveryCollectionEndpointRejectsForeignWorkspaceContext(string path, string method)
    {
        var a = await Owner(); var b = await Owner();
        using var ca = a.Client; using var cb = b.Client;
        var response = await Send(ca, "/api/workspace/" + path, new HttpMethod(method), new { }, b.Me.TenantId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ForeignIdsAndPayloadReferencesAreRejectedAndListsStayScoped()
    {
        var a = await Owner(); var b = await Owner();
        using var ca = a.Client; using var cb = b.Client;
        var branchesB = await Get<BranchDto[]>(cb, "branches", b.Me.TenantId);
        var rolesB = await Get<RoleDto[]>(cb, "roles", b.Me.TenantId);
        var workerB = rolesB.Single(r => r.Name == "Worker");
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, $"/api/workspace/branches/{branchesB[0].Id}", HttpMethod.Get, tenant: a.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, $"/api/workspace/branches/{branchesB[0].Id}", HttpMethod.Put, new BranchRequest("Stolen", true, branchesB[0].Version), a.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, $"/api/workspace/roles/{workerB.Id}", HttpMethod.Put, new RoleRequest("Stolen", [], workerB.Version), a.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, $"/api/workspace/members/{b.Me.MembershipId}", HttpMethod.Put, new MemberRequest(workerB.Id, false, [], 0), a.Me.TenantId)).StatusCode);
        var invite = new InviteRequest($"foreign-{Guid.NewGuid():N}@test.example", "Synthetic", workerB.Id, []);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, "/api/workspace/members", HttpMethod.Post, invite, a.Me.TenantId)).StatusCode);
        var rolesA = await Get<RoleDto[]>(ca, "roles", a.Me.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, "/api/workspace/members", HttpMethod.Post,
            invite with { RoleId = rolesA.Single(r => r.Name == "Worker").Id, BranchIds = [branchesB[0].Id] }, a.Me.TenantId)).StatusCode);
        var sessionsB = (await cb.GetFromJsonAsync<SessionDto[]>("/api/auth/sessions"))!;
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, $"/api/auth/sessions/{sessionsB[0].Id}", HttpMethod.Delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ca, "/api/auth/switch", HttpMethod.Post, new SwitchRequest(b.Me.MembershipId))).StatusCode);
        Assert.DoesNotContain((await ca.GetFromJsonAsync<MeDto>("/api/auth/me"))!.Workspaces, x => x.TenantId == b.Me.TenantId);
        Assert.DoesNotContain(await Get<BranchDto[]>(ca, "branches", a.Me.TenantId), x => branchesB.Any(y => y.Id == x.Id));
        Assert.DoesNotContain(rolesA, x => rolesB.Any(y => y.Id == x.Id));
        Assert.DoesNotContain(await Get<MemberDto[]>(ca, "members", a.Me.TenantId), x => x.Id == b.Me.MembershipId);
        Assert.DoesNotContain(await Get<AuditDto[]>(ca, "audit", a.Me.TenantId), x => x.ActorId == b.Me.UserId);
        Assert.Equal(a.Me.TenantId, (await Get<TenantDto>(ca, "tenant", a.Me.TenantId)).TenantId);
        var created = await Send(ca, "/api/workspace/branches", HttpMethod.Post, new { name = "Own only", tenantId = b.Me.TenantId }, a.Me.TenantId);
        created.EnsureSuccessStatusCode();
        Assert.DoesNotContain(await Get<BranchDto[]>(cb, "branches", b.Me.TenantId), x => x.Name == "Own only");
    }

    [Fact]
    public async Task WorkerPermissionsBranchGrantsAndRevocationAreEnforced()
    {
        var owner = await Owner(); using var ca = owner.Client;
        var roles = await Get<RoleDto[]>(ca, "roles", owner.Me.TenantId);
        var branch = (await Get<BranchDto[]>(ca, "branches", owner.Me.TenantId))[0];
        var worker = roles.Single(r => r.Name == "Worker");
        var email = $"worker-{Guid.NewGuid():N}@test.example";
        (await Send(ca, "/api/workspace/members", HttpMethod.Post, new InviteRequest(email, "Test worker", worker.Id, [branch.Id]), owner.Me.TenantId)).EnsureSuccessStatusCode();
        using var cw = Client();
        (await Send(cw, "/api/auth/reset", HttpMethod.Post, new ResetRequest(email, messages.Token(email), Password))).EnsureSuccessStatusCode();
        (await Send(cw, "/api/auth/login", HttpMethod.Post, new LoginRequest(email, Password))).EnsureSuccessStatusCode();
        Assert.Single(await Get<BranchDto[]>(cw, "branches", owner.Me.TenantId));
        foreach (var path in new[] { "members", "roles", "audit" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(cw, "/api/workspace/" + path, HttpMethod.Get, tenant: owner.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(cw, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Unauthorized"), owner.Me.TenantId)).StatusCode);
        foreach (var endpoint in new[] { ("tenant", HttpMethod.Put), ($"branches/{branch.Id}", HttpMethod.Put),
            ("roles", HttpMethod.Post), ($"roles/{worker.Id}", HttpMethod.Put), ("members", HttpMethod.Post),
            ($"members/{owner.Me.MembershipId}", HttpMethod.Put) })
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(cw, "/api/workspace/" + endpoint.Item1, endpoint.Item2,
                new { name = "Denied", timeZone = "Asia/Karachi", permissions = Array.Empty<string>(), roleId = worker.Id, branchIds = Array.Empty<Guid>(), email, active = true, version = 0 }, owner.Me.TenantId)).StatusCode);
        var members = await Get<MemberDto[]>(ca, "members", owner.Me.TenantId);
        var member = members.Single(x => x.Email == email);
        (await Send(ca, $"/api/workspace/members/{member.Id}", HttpMethod.Put, new MemberRequest(worker.Id, false, [], member.Version), owner.Me.TenantId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await cw.GetAsync("/api/auth/me")).StatusCode);
        var ownerMember = members.Single(x => x.Id == owner.Me.MembershipId);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(ca, $"/api/workspace/members/{ownerMember.Id}", HttpMethod.Put, new MemberRequest(worker.Id, false, [], ownerMember.Version), owner.Me.TenantId)).StatusCode);
    }

    [Fact]
    public async Task BranchQuotaSurvivesConcurrentCreationAndStaleEditsFail()
    {
        var owner = await Owner(); using var ca = owner.Client;
        (await Send(ca, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Second"), owner.Me.TenantId)).EnsureSuccessStatusCode();
        var responses = await Task.WhenAll(Send(ca, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Third"), owner.Me.TenantId),
            Send(ca, "/api/workspace/branches", HttpMethod.Post, new BranchRequest("Fourth"), owner.Me.TenantId));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var branch = (await Get<BranchDto[]>(ca, "branches", owner.Me.TenantId))[0];
        var request = new BranchRequest("Renamed", true, branch.Version);
        (await Send(ca, $"/api/workspace/branches/{branch.Id}", HttpMethod.Put, request, owner.Me.TenantId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, $"/api/workspace/branches/{branch.Id}", HttpMethod.Put, request, owner.Me.TenantId)).StatusCode);
    }

    [Fact]
    public async Task PasswordResetRevokesSessionsAndCannotBeReplayed()
    {
        var owner = await Owner(); using var ca = owner.Client; using var reset = Client();
        (await Send(reset, "/api/auth/forgot", HttpMethod.Post, new EmailRequest(owner.Email))).EnsureSuccessStatusCode();
        var request = new ResetRequest(owner.Email, messages.Token(owner.Email), "Replacement-Synthetic-2026!");
        (await Send(reset, "/api/auth/reset", HttpMethod.Post, request)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await ca.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(reset, "/api/auth/reset", HttpMethod.Post, request)).StatusCode);
    }

    [Fact]
    public async Task RepeatedFailedLoginsLockAccount()
    {
        var owner = await Owner(); using var ca = owner.Client; using var attacker = Client();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized,
            (await Send(attacker, "/api/auth/login", HttpMethod.Post, new LoginRequest(owner.Email, "Wrong-Password!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(attacker, "/api/auth/login", HttpMethod.Post, new LoginRequest(owner.Email, Password))).StatusCode);
    }

    [Fact]
    public async Task DatabaseConstraintsAndWriteGuardRejectCrossTenantRecords()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var branchB = (await Get<BranchDto[]>(cb, "branches", b.Me.TenantId))[0];
        var options = new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var context = new Gas365DbContext(options, new TenantScope { TenantId = a.Me.TenantId });
        Assert.DoesNotContain(await context.Branches.ToArrayAsync(), x => x.TenantId == b.Me.TenantId);
        context.BranchAccess.Add(new BranchAccess { TenantId = a.Me.TenantId, MembershipId = a.Me.MembershipId, BranchId = branchB.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        context.Branches.Add(new Branch { TenantId = b.Me.TenantId, Name = "Forbidden" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task UserAndAdminLimitsCannotBeExceeded()
    {
        var owner = await Owner(); using var ca = owner.Client;
        await using (var context = new Gas365DbContext(new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options,
            new TenantScope { TenantId = owner.Me.TenantId }))
        {
            var tenant = await context.Tenants.SingleAsync(); tenant.MaxUsers = 2; tenant.MaxAdmins = 1;
            await context.SaveChangesAsync();
        }
        var roles = await Get<RoleDto[]>(ca, "roles", owner.Me.TenantId);
        var admin = new InviteRequest($"admin-{Guid.NewGuid():N}@test.example", "Test admin", roles.Single(x => x.Name == "Admin").Id, []);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, "/api/workspace/members", HttpMethod.Post, admin, owner.Me.TenantId)).StatusCode);
        var worker = admin with { RoleId = roles.Single(x => x.Name == "Worker").Id };
        (await Send(ca, "/api/workspace/members", HttpMethod.Post, worker, owner.Me.TenantId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, "/api/workspace/members", HttpMethod.Post, worker with { Email = $"extra-{Guid.NewGuid():N}@test.example" }, owner.Me.TenantId)).StatusCode);
        var workerRole = roles.Single(x => x.Name == "Worker");
        Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, $"/api/workspace/roles/{workerRole.Id}", HttpMethod.Put,
            new RoleRequest("Worker", [Permissions.TeamManage], workerRole.Version), owner.Me.TenantId)).StatusCode);
    }

    [Fact]
    public async Task SessionRevocationExpiryAndAuditAreEnforced()
    {
        var owner = await Owner(); using var ca = owner.Client;
        var sessions = (await ca.GetFromJsonAsync<SessionDto[]>("/api/auth/sessions"))!;
        (await Send(ca, $"/api/auth/sessions/{sessions[0].Id}", HttpMethod.Delete)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await ca.GetAsync("/api/auth/me")).StatusCode);
        (await Send(ca, "/api/auth/login", HttpMethod.Post, new LoginRequest(owner.Email, Password))).EnsureSuccessStatusCode();
        var events = await Get<AuditDto[]>(ca, "audit", owner.Me.TenantId);
        Assert.Contains(events, x => x.Action == "tenant.created");
        Assert.Contains(events, x => x.Action == "session.revoked");
        await using var context = new Gas365DbContext(new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options,
            new TenantScope { TenantId = owner.Me.TenantId });
        var session = await context.LoginSessions.SingleAsync(x => !x.Revoked);
        session.LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-31);
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await ca.GetAsync("/api/auth/me")).StatusCode);
        var audit = await context.AuditEvents.FirstAsync(); audit.Action = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task WorkspaceSwitchRequiresOwnMembershipAndRefreshesContext()
    {
        var a = await Owner(); var b = await Owner(); using var ca = a.Client; using var cb = b.Client;
        var roles = await Get<RoleDto[]>(cb, "roles", b.Me.TenantId);
        var worker = roles.Single(x => x.Name == "Worker");
        (await Send(cb, "/api/workspace/members", HttpMethod.Post,
            new InviteRequest(a.Email, "Existing owner", worker.Id, []), b.Me.TenantId)).EnsureSuccessStatusCode();
        var me = (await ca.GetFromJsonAsync<MeDto>("/api/auth/me"))!;
        Assert.Equal(2, me.Workspaces.Length);
        var membership = me.Workspaces.Single(x => x.TenantId == b.Me.TenantId);
        (await Send(ca, "/api/auth/switch", HttpMethod.Post, new SwitchRequest(membership.MembershipId))).EnsureSuccessStatusCode();
        Assert.Equal(b.Me.TenantId, (await ca.GetFromJsonAsync<MeDto>("/api/auth/me"))!.TenantId);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(ca, "/api/workspace/tenant", HttpMethod.Get, tenant: a.Me.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(ca, "/api/workspace/members", HttpMethod.Get, tenant: b.Me.TenantId)).StatusCode);
        (await Send(cb, $"/api/workspace/roles/{worker.Id}", HttpMethod.Put,
            new RoleRequest("Worker", [Permissions.AuditView], worker.Version), b.Me.TenantId)).EnsureSuccessStatusCode();
        (await Send(ca, "/api/workspace/audit", HttpMethod.Get, tenant: b.Me.TenantId)).EnsureSuccessStatusCode();
        (await Send(ca, "/api/auth/switch", HttpMethod.Post, new SwitchRequest(a.Me.MembershipId))).EnsureSuccessStatusCode();
        Assert.True((await ca.GetFromJsonAsync<MeDto>("/api/auth/me"))!.IsOwner);
    }

    [Fact]
    public async Task ProductionCookiesRequireHttpsAndAuthBurstIsLimited()
    {
        var owner = await Owner(); using var ca = owner.Client;
        await using var production = application.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = await Send(client, "/api/auth/login", HttpMethod.Post, new LoginRequest(owner.Email, Password));
        login.EnsureSuccessStatusCode();
        var cookie = string.Join(';', login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-Gas365.Session", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        var statuses = new List<HttpStatusCode>();
        // Credential attempts carry the strict budget; session checks such as /csrf have their own (see RateLimitTests).
        for (var i = 0; i < 61; i++) statuses.Add((await Send(client, "/api/auth/login", HttpMethod.Post, new LoginRequest($"burst-{i}@test.example", Password))).StatusCode);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
