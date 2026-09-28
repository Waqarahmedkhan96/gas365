using System.Net;
using System.Net.Http.Json;
using Gas365.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Gas365.IntegrationTests;

// Owns its application instance so exhausting the credential budget cannot affect other test classes.
public sealed class RateLimitTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
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
        });
    }
    public async Task DisposeAsync() => await application.DisposeAsync();

    [Fact]
    public async Task SessionChecksHaveTheirOwnBudgetWhileCredentialAttemptsStayStrict()
    {
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/csrf")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }
        var token = (await client.GetFromJsonAsync<Dictionary<string, string>>("/api/auth/csrf"))!["token"];
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 61; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new LoginRequest($"missing-{i}@test.example", "Synthetic-Only-2026!")) };
            request.Headers.Add("X-CSRF-TOKEN", token);
            statuses.Add((await client.SendAsync(request)).StatusCode);
        }
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(60));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}
