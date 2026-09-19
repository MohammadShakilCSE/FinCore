using FinCore.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinCore.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _databaseName = "fincore_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private string? _testConnection;

    public FinCoreDbContext CreateContext() => new(new DbContextOptionsBuilder<FinCoreDbContext>()
        .UseNpgsql(_testConnection ?? throw new InvalidOperationException("Set FinCore_TestConnectionString."))
        .Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("FinCore_TestConnectionString");
        if (string.IsNullOrWhiteSpace(configured)) return; // PostgreSQL tests are explicitly skipped.
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        _adminConnection = builder.ConnectionString;
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        builder.Database = _databaseName;
        _testConnection = builder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_adminConnection is null) return;
        // The name is generated here, never taken from the supplied connection string.
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FinCore_TestConnectionString")))
            Skip = "Set FinCore_TestConnectionString to run against real PostgreSQL (requires CREATEDB).";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FinCore_TestConnectionString")))
            Skip = "Set FinCore_TestConnectionString to run against real PostgreSQL (requires CREATEDB).";
    }
}
