using Gas365.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gas365.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly string databaseName = "gas365_test_" + Guid.NewGuid().ToString("N");
    private string adminConnection = string.Empty;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("GAS365_TEST_DATABASE")
            ?? throw new InvalidOperationException("Set GAS365_TEST_DATABASE to a local PostgreSQL connection with CREATE DATABASE permission. Integration tests never substitute an in-memory database.");
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
        await command.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }.ConnectionString;
    }

    public Gas365DbContext CreateContext() => new(
        new DbContextOptionsBuilder<Gas365DbContext>().UseNpgsql(ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            return;
        }
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {databaseName} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
