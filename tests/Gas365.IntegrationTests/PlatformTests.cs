using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gas365.Api;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Gas365.IntegrationTests;

public sealed class PlatformTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private WebApplicationFactory<Program> CreateApplication(string? connection = null, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = connection ?? database.ConnectionString,
                    // Non-development environments require complete settings to start.
                    ["DataProtection:KeyPath"] = Path.Combine(Path.GetTempPath(), "gas365-test-keys"),
                    ["Mail:Host"] = "smtp.example.test",
                    ["Mail:From"] = "noreply@example.test",
                    ["Mail:PublicOrigin"] = "https://gas365.example.test",
                    ["Platform:AdminEmails"] = "admin@example.test"
                }));
        });

    [Fact]
    public async Task BaselineMigrationAppliesToEmptyDatabaseAndIsRepeatable()
    {
        await using var context = database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync("20260916104311_InitialBaseline");
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
        await context.GetService<IMigrator>().MigrateAsync("20260916153300_IdentityAndTenancy");
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(database.ConnectionString).Options;
        await using (var prior = new Gas365DbContext(options, new TenantScope { TenantId = tenantId }))
        {
            // Raw SQL: later migrations add tenant columns that do not exist at this schema version.
            await prior.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Tenants\" (\"TenantId\", \"Name\", \"TimeZone\", \"MaxBranches\", \"MaxUsers\", \"MaxAdmins\") VALUES ({tenantId}, 'Upgrade tenant', 'Asia/Karachi', 3, 10, 3)");
            prior.TenantRoles.Add(new TenantRole { TenantId = tenantId, Name = "Owner", IsOwner = true, Permissions = [Permissions.TenantManage] });
            await prior.SaveChangesAsync();
        }
        await context.GetService<IMigrator>().MigrateAsync("20260917201414_InventoryGuards");
        Guid stockId;
        await using (var phase04 = new Gas365DbContext(options, new TenantScope { TenantId = tenantId }))
        {
            var branch = new Branch { TenantId = tenantId, Name = "Existing branch" };
            var unit = new StockUnit { TenantId = tenantId, Code = "KG", DecimalPlaces = 3 };
            var item = new StockItem { TenantId = tenantId, Sku = "OLD", Name = "Existing stock", UnitId = unit.Id, Kind = "Lpg" };
            var warehouse = new Warehouse { TenantId = tenantId, BranchId = branch.Id, Name = "Existing warehouse" };
            var document = new StockDocument { TenantId = tenantId, BranchId = branch.Id, Kind = "Opening", Reason = "Historical reconciliation", IdempotencyKey = "prior-phase-opening", RequestHash = "original", PostedAt = DateTimeOffset.UtcNow, BusinessDate = new DateOnly(2026, 9, 18) };
            var movement = new StockMovement { TenantId = tenantId, BranchId = branch.Id, WarehouseId = warehouse.Id, ItemId = item.Id, DocumentId = document.Id, Quantity = 10, Value = 1234.56m, UnitCost = 123.456m };
            stockId = movement.Id;
            phase04.AddRange(branch, unit, item, warehouse, document, movement); await phase04.SaveChangesAsync();
        }
        await context.GetService<IMigrator>().MigrateAsync("20260919072004_Purchasing");
        Guid supplierId; var historicalPurchaseId = Guid.NewGuid();
        await using (var phase05 = new Gas365DbContext(options, new TenantScope { TenantId = tenantId }))
        {
            var supplier = new Supplier { TenantId = tenantId, Name = "Retained supplier", Address = "Existing address" };
            supplierId = supplier.Id; phase05.Suppliers.Add(supplier); await phase05.SaveChangesAsync();
            var branchId = await phase05.Branches.Select(x => x.Id).SingleAsync();
            var stockDocumentId = await phase05.StockDocuments.Select(x => x.Id).SingleAsync();
            var draftId = Guid.NewGuid();
            await phase05.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"PurchaseDrafts\" (\"TenantId\", \"Id\", \"BranchId\", \"SupplierId\", \"Content\", \"Snapshot\") VALUES ({tenantId}, {draftId}, {branchId}, {supplierId}, '{{}}'::jsonb, '{{}}'::jsonb)");
            await phase05.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"PurchaseDocuments\" (\"TenantId\", \"Id\", \"BranchId\", \"SupplierId\", \"DraftId\", \"StockDocumentId\", \"SupplierName\", \"Content\", \"Total\") VALUES ({tenantId}, {historicalPurchaseId}, {branchId}, {supplierId}, {draftId}, {stockDocumentId}, 'Retained supplier', '{{}}'::jsonb, 1060)");
        }
        await context.GetService<IMigrator>().MigrateAsync("20260928120055_FinanceAndRegisters");
        var accountId = Guid.NewGuid(); var categoryId = Guid.NewGuid();
        await using (var phase07 = new Gas365DbContext(options, new TenantScope { TenantId = tenantId }))
        {
            // Raw SQL: the current model already carries later columns that do not exist at this schema version.
            var branchId = await phase07.Branches.Select(x => x.Id).SingleAsync();
            await phase07.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"MoneyAccounts\" (\"TenantId\", \"Id\", \"BranchId\", \"Name\", \"Kind\", \"DefaultCash\", \"CreatedAt\") VALUES ({tenantId}, {accountId}, {branchId}, 'Existing drawer', 'Cash', TRUE, now())");
            await phase07.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ExpenseCategories\" (\"TenantId\", \"Id\", \"Name\", \"Capital\") VALUES ({tenantId}, {categoryId}, 'Existing category', FALSE)");
        }
        await context.Database.MigrateAsync();
        await using (var upgraded = new Gas365DbContext(options, new TenantScope { TenantId = tenantId }))
        {
            Assert.Equal("Upgrade tenant", (await upgraded.Tenants.SingleAsync()).Name);
            Assert.Contains(Permissions.InventoryPost, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.SalesDraft, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.CustomerManage, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.PriceManage, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.PurchasePost, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.TenantManage, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            var movement = await upgraded.StockMovements.SingleAsync(x => x.Id == stockId);
            Assert.Equal(10, movement.Quantity); Assert.Equal(1234.56m, movement.Value); Assert.Equal(123.456m, movement.UnitCost);
            var historicalPurchase = await upgraded.PurchaseDocuments.SingleAsync(x => x.Id == historicalPurchaseId);
            Assert.Equal(1060, historicalPurchase.Total); Assert.False(historicalPurchase.SeparateCosts);
            Assert.Contains(Permissions.CylinderReturn, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.SalesPost, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Equal("Retained supplier", (await upgraded.Suppliers.SingleAsync(x => x.Id == supplierId)).Name);
            Assert.Empty(await upgraded.Customers.ToArrayAsync());
            Assert.Empty(await upgraded.PriceLists.ToArrayAsync());
            var account = await upgraded.MoneyAccounts.SingleAsync(); Assert.Equal(accountId, account.Id); Assert.True(account.Active); Assert.True(account.DefaultCash);
            var category = await upgraded.ExpenseCategories.SingleAsync(); Assert.Equal(categoryId, category.Id); Assert.True(category.Active);
            Assert.Empty(await upgraded.FinanceEntries.ToArrayAsync());
            Assert.Contains(Permissions.FinancePost, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Contains(Permissions.ReportsExport, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            Assert.Empty(await upgraded.ExportJobs.ToArrayAsync());
            Assert.Contains(Permissions.ImportsManage, (await upgraded.TenantRoles.SingleAsync()).Permissions);
            var settings = await upgraded.Tenants.SingleAsync(); Assert.Equal(30, settings.OverdueCreditDays); Assert.Equal(new TimeOnly(21, 0), settings.ClosingReminder);
            // Existing businesses are grandfathered as active pilots on the Business plan.
            Assert.Equal(SubscriptionRules.BusinessId, settings.PlanId); Assert.Equal(SubscriptionRules.Active, settings.SubscriptionStatus); Assert.Null(settings.PaidThrough);
            Assert.Equal(3, await upgraded.Plans.CountAsync());
        }
        Assert.Equal(15, (await context.Database.GetAppliedMigrationsAsync()).Count());
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
