using FinCore.Application.Abstractions.Identity;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Application.Features.Wallets.DemoDeposit;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Domain.ValueObjects;
using FinCore.Infrastructure;
using FinCore.Infrastructure.Persistence.Context;
using FinCore.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.IntegrationTests;

public sealed class WalletConcurrencyTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private async Task<Wallet> SeedAsync(decimal amount = 0)
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        if (amount > 0) wallet.Credit(new Money(amount, "BDT"));
        await using var context = database.CreateContext();
        await new WalletRepository(context).AddAsync(wallet);
        return wallet;
    }

    [PostgresFact]
    public async Task Stale_debit_fails_and_preserves_winning_balance_and_version()
    {
        var wallet = await SeedAsync(1000); // Funding increments version: initial persisted version is 2.
        await using var a = database.CreateContext();
        await using var b = database.CreateContext();
        var first = (await new WalletRepository(a).GetTrackedByIdAsync(wallet.Id))!;
        var stale = (await new WalletRepository(b).GetTrackedByIdAsync(wallet.Id))!;
        Assert.Equal(first.Version, stale.Version);
        first.Debit(new Money(800, "BDT"));
        await new WalletRepository(a).SaveChangesAsync();
        stale.Debit(new Money(700, "BDT"));
        var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new WalletRepository(b).SaveChangesAsync());
        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);
        await using var verify = database.CreateContext();
        var stored = await verify.Wallets.SingleAsync(item => item.Id == wallet.Id);
        Assert.Equal(200, stored.Balance.Amount);
        Assert.Equal(wallet.Version + 1, stored.Version);
    }

    [PostgresFact]
    public async Task Competing_transfers_share_scoped_context_and_failed_transfer_rolls_back_everything()
    {
        var sender = await SeedAsync(1000);
        var receiverA = await SeedAsync();
        var receiverB = await SeedAsync();
        await using var setup = database.CreateContext();
        var services = new ServiceCollection();
        services.AddInfrastructure(setup.Database.GetConnectionString()!);
        services.AddScoped<TransferMoneyHandler>();
        services.AddScoped<ICurrentOwner>(_ => new TestOwner(sender.OwnerId));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scopeA = provider.CreateAsyncScope();
        await using var scopeB = provider.CreateAsyncScope();
        var contextA = scopeA.ServiceProvider.GetRequiredService<FinCoreDbContext>();
        var contextB = scopeB.ServiceProvider.GetRequiredService<FinCoreDbContext>();
        // Deterministic overlap: both scopes read the sender before either operation saves.
        var first = await scopeA.ServiceProvider.GetRequiredService<IWalletRepository>().GetTrackedByIdAsync(sender.Id);
        var stale = await scopeB.ServiceProvider.GetRequiredService<IWalletRepository>().GetTrackedByIdAsync(sender.Id);
        Assert.NotSame(contextA, contextB);
        Assert.Equal(first!.Version, stale!.Version);
        var winner = await scopeA.ServiceProvider.GetRequiredService<TransferMoneyHandler>()
            .HandleAsync(new(sender.Id, receiverA.Id, 800, "BDT", "winner"));
        Assert.Contains(contextA.ChangeTracker.Entries<LedgerTransaction>(), entry => entry.Entity.Id == winner.TransactionId);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scopeB.ServiceProvider.GetRequiredService<TransferMoneyHandler>()
            .HandleAsync(new(sender.Id, receiverB.Id, 700, "BDT", "loser")));
        Assert.Empty(contextB.ChangeTracker.Entries());

        await using var verify = database.CreateContext();
        var stored = await verify.Wallets.Where(item => item.Id == sender.Id || item.Id == receiverA.Id || item.Id == receiverB.Id).ToListAsync();
        Assert.Equal(200, stored.Single(item => item.Id == sender.Id).Balance.Amount);
        Assert.Equal(800, stored.Single(item => item.Id == receiverA.Id).Balance.Amount);
        Assert.Equal(0, stored.Single(item => item.Id == receiverB.Id).Balance.Amount);
        Assert.Equal(receiverB.Version, stored.Single(item => item.Id == receiverB.Id).Version);
        Assert.Equal(1000, stored.Sum(item => item.Balance.Amount));
        Assert.Single(await verify.IdempotencyRecords.Where(item => item.OwnerId == sender.OwnerId).ToListAsync());
        Assert.False(await verify.LedgerEntries.AnyAsync(item => item.WalletId == receiverB.Id));
        var ledger = await verify.LedgerTransactions.Include(item => item.Entries).SingleAsync(item => item.Id == winner.TransactionId);
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(800, ledger.Entries.Single(item => item.EntryType == LedgerEntryType.Debit).Amount.Amount);
        Assert.Equal(800, ledger.Entries.Single(item => item.EntryType == LedgerEntryType.Credit).Amount.Amount);
    }

    [PostgresTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deposit_and_transfer_cannot_overwrite_each_other(bool depositWins)
    {
        var sender = await SeedAsync(1000);
        var receiver = await SeedAsync();
        await using var a = database.CreateContext();
        await using var b = database.CreateContext();
        var walletA = new WalletRepository(a);
        var walletB = new WalletRepository(b);
        await walletA.GetTrackedByIdAsync(sender.Id);
        await walletB.GetTrackedByIdAsync(sender.Id);
        var deposit = new DemoDepositHandler(depositWins ? walletA : walletB);
        var transfer = new TransferMoneyHandler(depositWins ? walletB : walletA,
            new LedgerTransactionRepository(depositWins ? b : a),
            new IdempotencyRepository(depositWins ? b : a), new TestOwner(sender.OwnerId));
        if (depositWins)
        {
            await deposit.HandleAsync(new(sender.Id, 100, "BDT"));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => transfer.HandleAsync(new(sender.Id, receiver.Id, 800, "BDT", "transfer-key")));
        }
        else
        {
            await transfer.HandleAsync(new(sender.Id, receiver.Id, 800, "BDT", "transfer-key"));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => deposit.HandleAsync(new(sender.Id, 100, "BDT")));
        }
        await using var verify = database.CreateContext();
        Assert.Equal(depositWins ? 1100 : 200, (await verify.Wallets.SingleAsync(item => item.Id == sender.Id)).Balance.Amount);
        Assert.Equal(depositWins ? 0 : 800, (await verify.Wallets.SingleAsync(item => item.Id == receiver.Id)).Balance.Amount);
        Assert.Equal(depositWins ? 0 : 2, await verify.LedgerEntries.CountAsync(item => item.WalletId == sender.Id || item.WalletId == receiver.Id));
    }

    [PostgresFact]
    public async Task Status_change_conflicts_with_stale_credit()
    {
        var wallet = await SeedAsync();
        await using var a = database.CreateContext();
        await using var b = database.CreateContext();
        var first = (await new WalletRepository(a).GetTrackedByIdAsync(wallet.Id))!;
        var stale = (await new WalletRepository(b).GetTrackedByIdAsync(wallet.Id))!;
        first.Suspend();
        await a.SaveChangesAsync();
        stale.Credit(new Money(50, "BDT"));
        Assert.Throws<ConcurrencyConflictException>(() => b.SaveChanges());
        await using var verify = database.CreateContext();
        var stored = await verify.Wallets.SingleAsync(item => item.Id == wallet.Id);
        Assert.Equal(WalletStatus.Suspended, stored.Status);
        Assert.Equal(0, stored.Balance.Amount);
        Assert.Equal(2, stored.Version);
    }
}
