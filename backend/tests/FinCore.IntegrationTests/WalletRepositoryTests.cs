using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Infrastructure.Persistence.Repositories;
using FinCore.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinCore.IntegrationTests;

public sealed class WalletRepositoryTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task Add_commits_and_fresh_context_reads_all_values_without_tracking()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), " bdt ");
        wallet.Credit(new Money(1234.5678m, "BDT"));
        wallet.Suspend();
        await using (var write = database.CreateContext())
            await new WalletRepository(write).AddAsync(wallet);

        await using var read = database.CreateContext();
        var saved = await new WalletRepository(read).GetByIdAsync(wallet.Id);
        Assert.NotNull(saved);
        Assert.Equal(wallet.Id, saved.Id);
        Assert.Equal(wallet.OwnerId, saved.OwnerId);
        Assert.Equal("BDT", saved.Currency);
        Assert.Equal(wallet.Balance, saved.Balance);
        Assert.Equal(WalletStatus.Suspended, saved.Status);
        // PostgreSQL timestamps have microsecond precision; .NET ticks have 100ns precision.
        Assert.InRange(Math.Abs((wallet.CreatedAt - saved.CreatedAt).Ticks), 0, 9);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.Empty(read.ChangeTracker.Entries());
    }

    [PostgresFact]
    public async Task Missing_wallet_returns_null()
    {
        await using var context = database.CreateContext();
        Assert.Null(await new WalletRepository(context).GetByIdAsync(Guid.NewGuid()));
    }

    [PostgresFact]
    public async Task Primary_key_rejects_duplicate_wallet()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "USD");
        await using (var first = database.CreateContext())
            await new WalletRepository(first).AddAsync(wallet);
        await using var second = database.CreateContext();
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => new WalletRepository(second).AddAsync(wallet));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task Tracked_wallet_detects_replacement_of_immutable_money()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "USD");
        await using (var write = database.CreateContext())
        {
            await new WalletRepository(write).AddAsync(wallet);
            wallet.Credit(new Money(10.1234m, "USD"));
            await write.SaveChangesAsync();
        }
        await using var read = database.CreateContext();
        Assert.Equal(10.1234m, (await new WalletRepository(read).GetByIdAsync(wallet.Id))!.Balance.Amount);
    }

    [PostgresTheory]
    [InlineData("\"OwnerId\" = '00000000-0000-0000-0000-000000000000'", "23514")]
    [InlineData("\"OwnerId\" = NULL", "23502")]
    [InlineData("\"Currency\" = NULL", "23502")]
    [InlineData("\"Currency\" = 'usd'", "23514")]
    [InlineData("\"Currency\" = 'US'", "23514")]
    [InlineData("\"Currency\" = 'USDD'", "22001")]
    [InlineData("\"Balance\" = -1", "23514")]
    [InlineData("\"Balance\" = NULL", "23502")]
    [InlineData("\"Balance\" = 1000000000000000", "22003")]
    [InlineData("\"BalanceCurrency\" = 'BDT'", "23514")]
    [InlineData("\"BalanceCurrency\" = NULL", "23502")]
    [InlineData("\"Status\" = 'Unknown'", "23514")]
    [InlineData("\"CreatedAt\" = NULL", "23502")]
    public async Task Database_rejects_invalid_rows_even_when_domain_is_bypassed(string assignment, string sqlState)
    {
        await using var context = database.CreateContext();
        var wallet = Wallet.Create(Guid.NewGuid(), "USD");
        await new WalletRepository(context).AddAsync(wallet);
        // SQL fragments are fixed test cases; the wallet ID is parameterized.
        var sql = "UPDATE \"Wallets\" SET " + assignment + " WHERE \"Id\" = {0}";
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, wallet.Id));
        Assert.Equal(sqlState, error.SqlState);
    }
}
