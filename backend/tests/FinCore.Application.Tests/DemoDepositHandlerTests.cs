using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Features.Wallets.DemoDeposit;
using FinCore.Domain.Entities;
using FinCore.Domain.Exceptions;
using FinCore.valueObjects;

namespace FinCore.Application.Tests;

public sealed class DemoDepositHandlerTests
{
    [Fact]
    public async Task Deposit_adds_to_existing_balance_and_saves_before_returning()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        wallet.Credit(new Money(1000, "BDT"));
        var repository = new TestWalletRepository(wallet);
        using var cancellation = new CancellationTokenSource();

        var result = await new DemoDepositHandler(repository).HandleAsync(
            new DemoDepositCommand(wallet.Id, 500, " bdt "), cancellation.Token);

        Assert.Equal(new DemoDepositResult(wallet.Id, 500, 1500, "BDT"), result);
        Assert.Equal(1, repository.SaveCount);
        Assert.Equal(cancellation.Token, repository.LoadToken);
        Assert.Equal(cancellation.Token, repository.SaveToken);
    }

    [Theory]
    [InlineData(0, "BDT")]
    [InlineData(-1, "BDT")]
    [InlineData(500, "USD")]
    [InlineData(500, "")]
    public async Task Invalid_money_does_not_save_or_change_balance(decimal amount, string currency)
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        wallet.Credit(new Money(1000, "BDT"));
        var repository = new TestWalletRepository(wallet);

        await Assert.ThrowsAsync<DomainException>(() => new DemoDepositHandler(repository).HandleAsync(
            new DemoDepositCommand(wallet.Id, amount, currency)));

        Assert.Equal(1000, wallet.Balance.Amount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Suspended_or_closed_wallet_does_not_save(bool close)
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        if (close) wallet.Close(); else wallet.Suspend();
        var repository = new TestWalletRepository(wallet);

        await Assert.ThrowsAsync<DomainException>(() => new DemoDepositHandler(repository).HandleAsync(
            new DemoDepositCommand(wallet.Id, 500, "BDT")));

        Assert.Equal(0, wallet.Balance.Amount);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Missing_wallet_returns_null_without_saving()
    {
        var repository = new TestWalletRepository(null);
        var result = await new DemoDepositHandler(repository).HandleAsync(
            new DemoDepositCommand(Guid.NewGuid(), 500, "BDT"));
        Assert.Null(result);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Save_failure_propagates_instead_of_returning_success()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        var repository = new TestWalletRepository(wallet) { FailSave = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DemoDepositHandler(repository).HandleAsync(
            new DemoDepositCommand(wallet.Id, 500, "BDT")));
    }

    private sealed class TestWalletRepository(Wallet? wallet) : IWalletRepository
    {
        public int SaveCount { get; private set; }
        public bool FailSave { get; init; }
        public CancellationToken LoadToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }

        public Task<Wallet?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            LoadToken = cancellationToken;
            return Task.FromResult(wallet?.Id == id ? wallet : null);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveToken = cancellationToken;
            if (FailSave) throw new InvalidOperationException("Simulated persistence failure.");
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(Wallet value, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Deposit must not add a new wallet.");

        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Deposit must use a tracked lookup.");
    }
}
