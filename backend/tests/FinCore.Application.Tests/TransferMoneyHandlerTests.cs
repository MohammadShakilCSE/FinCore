using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Application.Tests;

public sealed class TransferMoneyHandlerTests
{
    [Fact]
    public async Task Success_changes_balances_stages_balanced_ledger_and_saves_once()
    {
        var sender = FundedWallet(1000);
        var receiver = FundedWallet(100);
        var calls = new List<string>();
        var wallets = new WalletStub([sender, receiver], calls);
        var ledger = new LedgerStub(calls);
        using var cancellation = new CancellationTokenSource();

        var result = await new TransferMoneyHandler(wallets, ledger, new MemoryIdempotencyRepository(), new TestOwner(sender.OwnerId)).HandleAsync(
            new(sender.Id, receiver.Id, 250, " bdt ", "unit-key"), cancellation.Token);

        Assert.Equal(750, sender.Balance.Amount);
        Assert.Equal(350, receiver.Balance.Amount);
        var transaction = Assert.Single(ledger.Transactions);
        Assert.Equal(LedgerTransactionStatus.Posted, transaction.Status);
        Assert.Equal(2, transaction.Entries.Count);
        var debit = Assert.Single(transaction.Entries, entry => entry.EntryType == LedgerEntryType.Debit);
        var credit = Assert.Single(transaction.Entries, entry => entry.EntryType == LedgerEntryType.Credit);
        Assert.Equal(sender.Id, debit.WalletId);
        Assert.Equal(receiver.Id, credit.WalletId);
        Assert.Equal(new Money(250, "BDT"), debit.Amount);
        Assert.Equal(debit.Amount, credit.Amount);
        Assert.All(transaction.Entries, entry => Assert.Equal(transaction.Id, entry.LedgerTransactionId));
        Assert.Equal(new TransferMoneyResult(transaction.Id, transaction.Reference, 250, "BDT"), result);
        Assert.False(string.IsNullOrWhiteSpace(result.Reference));
        Assert.Equal(new[] { "load", "load", "ledger", "save" }, calls);
        Assert.Equal(1, wallets.SaveCount);
        Assert.Equal(1, ledger.AddCount);
        Assert.All(wallets.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(cancellation.Token, ledger.Token);
    }

    [Theory]
    [InlineData("empty-sender")]
    [InlineData("empty-receiver")]
    [InlineData("same-wallet")]
    [InlineData("missing-sender")]
    [InlineData("missing-receiver")]
    [InlineData("insufficient")]
    [InlineData("sender-currency")]
    [InlineData("receiver-currency")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("sender-suspended")]
    [InlineData("receiver-suspended")]
    [InlineData("sender-closed")]
    [InlineData("receiver-closed")]
    [InlineData("receiver-overflow")]
    public async Task Validation_failure_never_stages_ledger_or_saves(string scenario)
    {
        var sender = FundedWallet(scenario == "sender-closed" ? 0 : 1000);
        var receiver = FundedWallet(0, scenario == "receiver-currency" ? "USD" : "BDT");
        if (scenario == "sender-suspended") sender.Suspend();
        if (scenario == "receiver-suspended") receiver.Suspend();
        if (scenario == "sender-closed") sender.Close();
        if (scenario == "receiver-closed") receiver.Close();
        if (scenario == "receiver-overflow") receiver.Credit(new Money(999_999_999_999_999.9999m, "BDT"));
        var senderId = scenario == "empty-sender" ? Guid.Empty : sender.Id;
        var receiverId = scenario switch
        {
            "empty-receiver" => Guid.Empty,
            "same-wallet" => sender.Id,
            _ => receiver.Id
        };
        var amount = scenario switch { "zero" => 0, "negative" => -1, "insufficient" => 1001, _ => 250 };
        var currency = scenario == "sender-currency" ? "USD" : "BDT";
        var available = new List<Wallet>();
        if (scenario != "missing-sender") available.Add(sender);
        if (scenario != "missing-receiver") available.Add(receiver);
        var calls = new List<string>();
        var wallets = new WalletStub(available, calls);
        var ledger = new LedgerStub(calls);

        await Assert.ThrowsAsync<DomainException>(() => new TransferMoneyHandler(wallets, ledger, new MemoryIdempotencyRepository(), new TestOwner(sender.OwnerId))
            .HandleAsync(new(senderId, receiverId, amount, currency, "unit-key")));

        Assert.Equal(0, wallets.SaveCount);
        Assert.Equal(0, ledger.AddCount);
        Assert.Empty(ledger.Transactions);
        if (scenario is "empty-sender" or "empty-receiver" or "same-wallet") Assert.Empty(calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Persistence_failure_propagates_without_returning_success(bool failLedger)
    {
        var sender = FundedWallet(1000);
        var receiver = FundedWallet(0);
        var calls = new List<string>();
        var wallets = new WalletStub([sender, receiver], calls) { FailSave = !failLedger };
        var ledger = new LedgerStub(calls) { FailAdd = failLedger };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TransferMoneyHandler(wallets, ledger, new MemoryIdempotencyRepository(), new TestOwner(sender.OwnerId))
            .HandleAsync(new(sender.Id, receiver.Id, 250, "BDT", "unit-key")));
        Assert.Equal(failLedger ? 0 : 1, wallets.SaveCount);
        Assert.Equal(1, ledger.AddCount);
    }

    private static Wallet FundedWallet(decimal amount, string currency = "BDT")
    {
        var wallet = Wallet.Create(Guid.NewGuid(), currency);
        if (amount > 0) wallet.Credit(new Money(amount, currency));
        return wallet;
    }

    private sealed class WalletStub(IEnumerable<Wallet> wallets, List<string> calls) : IWalletRepository
    {
        public int SaveCount { get; private set; }
        public bool FailSave { get; init; }
        public List<CancellationToken> Tokens { get; } = [];
        public Task<Wallet?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            calls.Add("load");
            Tokens.Add(cancellationToken);
            return Task.FromResult(wallets.SingleOrDefault(wallet => wallet.Id == id));
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            calls.Add("save");
            Tokens.Add(cancellationToken);
            SaveCount++;
            if (FailSave) throw new InvalidOperationException("Simulated save failure.");
            return Task.CompletedTask;
        }
        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("A transfer must not create a wallet.");
        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(wallets.SingleOrDefault(wallet => wallet.Id == id));
    }

    private sealed class LedgerStub(List<string> calls) : ILedgerTransactionRepository
    {
        public List<LedgerTransaction> Transactions { get; } = [];
        public int AddCount { get; private set; }
        public bool FailAdd { get; init; }
        public CancellationToken Token { get; private set; }
        public Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default)
        {
            calls.Add("ledger");
            AddCount++;
            Token = cancellationToken;
            if (FailAdd) throw new InvalidOperationException("Simulated staging failure.");
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }
    }
}
