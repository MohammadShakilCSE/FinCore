using FinCore.Application.Features.Wallets.DemoDeposit;
using FinCore.Domain.Entities;
using FinCore.Domain.Exceptions;
using FinCore.Infrastructure.Persistence.Repositories;
using FinCore.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace FinCore.IntegrationTests;

public sealed class DemoDepositTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task Deposit_commits_balance_visible_from_a_fresh_context()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        wallet.Credit(new Money(1000, "BDT"));
        await using (var create = database.CreateContext())
            await new WalletRepository(create).AddAsync(wallet);

        await using (var deposit = database.CreateContext())
        {
            var repository = new WalletRepository(deposit);
            var result = await new DemoDepositHandler(repository).HandleAsync(
                new DemoDepositCommand(wallet.Id, 500.1234m, "BDT"));
            Assert.Equal(new DemoDepositResult(wallet.Id, 500.1234m, 1500.1234m, "BDT"), result);
            Assert.Contains(deposit.ChangeTracker.Entries<Wallet>(), entry => entry.Entity.Id == wallet.Id);
            Assert.All(deposit.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        }

        await using var read = database.CreateContext();
        var stored = await new WalletRepository(read).GetByIdAsync(wallet.Id);
        Assert.NotNull(stored);
        Assert.Equal(1500.1234m, stored.Balance.Amount);
        Assert.Equal("BDT", stored.Balance.Currency);
        Assert.Empty(read.ChangeTracker.Entries());
    }

    [PostgresTheory]
    [InlineData(0, "BDT", "active")]
    [InlineData(-1, "BDT", "active")]
    [InlineData(500, "USD", "active")]
    [InlineData(500, "BDT", "suspended")]
    [InlineData(500, "BDT", "closed")]
    public async Task Rejected_deposit_leaves_stored_balance_unchanged(decimal amount, string currency, string state)
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        if (state == "suspended") wallet.Suspend();
        if (state == "closed") wallet.Close();
        await using (var create = database.CreateContext())
            await new WalletRepository(create).AddAsync(wallet);

        await using (var deposit = database.CreateContext())
            await Assert.ThrowsAsync<DomainException>(() => new DemoDepositHandler(new WalletRepository(deposit))
                .HandleAsync(new DemoDepositCommand(wallet.Id, amount, currency)));

        await using var read = database.CreateContext();
        Assert.Equal(0, (await new WalletRepository(read).GetByIdAsync(wallet.Id))!.Balance.Amount);
    }

    [PostgresFact]
    public async Task Missing_wallet_returns_null()
    {
        await using var context = database.CreateContext();
        Assert.Null(await new DemoDepositHandler(new WalletRepository(context))
            .HandleAsync(new DemoDepositCommand(Guid.NewGuid(), 500, "BDT")));
    }
}
