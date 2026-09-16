using Gas365.Api;
using Gas365.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

if (args.Contains("--health-probe"))
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    try
    {
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32768);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.AddAccess();

builder.Services.AddSingleton(services => NpgsqlDataSource.Create(
    services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database must be configured.")));
builder.Services.AddDbContext<Gas365DbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();
_ = app.Services.GetRequiredService<NpgsqlDataSource>();
if (args.Contains("--migrate"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider.GetRequiredService<Gas365DbContext>().Database.MigrateAsync();
    return;
}
app.UseExceptionHandler();
app.UseAccess();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(HealthResponse.From(report))
});
app.MapGet("/api/health", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
{
    var report = await healthChecks.CheckHealthAsync(cancellationToken);
    return Results.Json(HealthResponse.From(report), statusCode:
        report.Status == HealthStatus.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
})
    .WithName("GetPlatformHealth")
    .WithSummary("Check API and PostgreSQL readiness")
    .Produces<HealthResponse>()
    .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.RunAsync();

public partial class Program;
