using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gas365.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Gas365.IntegrationTests;

public sealed class PlatformTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private WebApplicationFactory<Program> CreateApplication(string? connection = null, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connection ?? database.ConnectionString }));
        });

    [Fact]
    public async Task BaselineMigrationAppliesToEmptyDatabaseAndIsRepeatable()
    {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/health/ready")]
    public async Task ReadinessChecksRealPostgres(string path)
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body!.Database);
    }

    [Fact]
    public async Task DatabaseFailureDoesNotTakeDownLivenessOrExposeConnectionDetails()
    {
        await using var application = CreateApplication("Host=127.0.0.1;Port=1;Database=private_database;Username=private_user;Password=private_password;Timeout=1");
        using var client = application.CreateClient();
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var text = await ready.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private_", text);
        Assert.Contains("Unhealthy", text);
    }

    [Fact]
    public async Task OpenApiDescribesHealthSuccessAndFailure()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var responses = document.RootElement.GetProperty("paths").GetProperty("/api/health").GetProperty("get").GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("503", out _));
    }

    [Fact]
    public async Task ProductionDoesNotExposeOpenApi()
    {
        await using var application = CreateApplication(environment: "Production");
        using var client = application.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
