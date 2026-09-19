using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinCore.IntegrationTests;

public sealed class LedgerPersistenceTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static LedgerTransaction CreateTransaction() => LedgerTransaction.CreateTransfer(
        Guid.NewGuid(), Guid.NewGuid(), new Money(123.4567m, "BDT"));

    [PostgresFact]
    public async Task Fresh_context_loads_private_entries_and_immutable_money()
    {
        var transaction = CreateTransaction();
        await using (var write = database.CreateContext())
        {
            write.LedgerTransactions.Add(transaction);
            await write.SaveChangesAsync();
        }

        await using var read = database.CreateContext();
        var loaded = await read.LedgerTransactions.Include(item => item.Entries)
            .SingleAsync(item => item.Id == transaction.Id);
        Assert.Equal(transaction.Reference, loaded.Reference);
        Assert.Equal(LedgerTransactionStatus.Posted, loaded.Status);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.InRange(Math.Abs((transaction.CreatedAt - loaded.CreatedAt).Ticks), 0, 9);
        Assert.Equal(2, loaded.Entries.Count);
        Assert.True(Assert.IsAssignableFrom<ICollection<LedgerEntry>>(loaded.Entries).IsReadOnly);
        foreach (var entry in loaded.Entries)
        {
            var original = Assert.Single(transaction.Entries, item => item.Id == entry.Id);
            Assert.Equal(transaction.Id, entry.LedgerTransactionId);
            Assert.Equal(original.WalletId, entry.WalletId);
            Assert.Equal(original.EntryType, entry.EntryType);
            Assert.Equal(new Money(123.4567m, "BDT"), entry.Amount);
            Assert.Equal(DateTimeKind.Utc, entry.CreatedAt.Kind);
        }
        Assert.Equal(new[] { LedgerEntryType.Credit, LedgerEntryType.Debit },
            loaded.Entries.Select(entry => entry.EntryType).Order().ToArray());
    }

    [PostgresTheory]
    [InlineData("\"Amount\" = 0", "23514")]
    [InlineData("\"Amount\" = -1", "23514")]
    [InlineData("\"Amount\" = NULL", "23502")]
    [InlineData("\"Currency\" = 'bdt'", "23514")]
    [InlineData("\"Currency\" = 'BD'", "23514")]
    [InlineData("\"Currency\" = NULL", "23502")]
    [InlineData("\"LedgerTransactionId\" = '00000000-0000-0000-0000-000000000000'", "23503")]
    public async Task Database_rejects_invalid_entry_rows(string assignment, string sqlState)
    {
        await using var context = database.CreateContext();
        var transaction = CreateTransaction();
        context.LedgerTransactions.Add(transaction);
        await context.SaveChangesAsync();
        var entryId = transaction.Entries.First().Id;
        var sql = "UPDATE \"LedgerEntries\" SET " + assignment + " WHERE \"Id\" = {0}";
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, entryId));
        Assert.Equal(sqlState, error.SqlState);
    }

    [PostgresFact]
    public async Task Reference_must_be_unique()
    {
        await using var context = database.CreateContext();
        var first = CreateTransaction();
        var second = CreateTransaction();
        context.AddRange(first, second);
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LedgerTransactions\" SET \"Reference\" = {first.Reference} WHERE \"Id\" = {second.Id}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [PostgresFact]
    public async Task Database_restricts_deleting_transaction_with_entries()
    {
        var transaction = CreateTransaction();
        await using (var write = database.CreateContext())
        {
            write.LedgerTransactions.Add(transaction);
            await write.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        // Direct SQL verifies the database restriction independently of EF's loaded graph.
        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"LedgerTransactions\" WHERE \"Id\" = {transaction.Id}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
        Assert.Equal(2, await context.LedgerEntries.CountAsync(entry => entry.LedgerTransactionId == transaction.Id));
    }
}
