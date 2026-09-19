using System.Globalization;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Application.Features.Transfers.GetTransferAttempt;
using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Application.Idempotency;
using FinCore.Domain.Entities;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Application.Tests;

public sealed class TransferIdempotencyTests
{
    [Fact]
    public async Task Replay_returns_original_without_more_wallet_or_ledger_writes_and_new_key_executes_again()
    {
        var fixture = new Fixture();
        var first = await fixture.Handler.HandleAsync(fixture.Command);
        var version = fixture.Sender.Version;
        var replay = await fixture.Handler.HandleAsync(fixture.Command with { Amount = 500.0000m, Currency = " bdt " });
        Assert.Equal(first, replay);
        Assert.Equal(version, fixture.Sender.Version);
        Assert.Equal(1, fixture.Repositories.Saves);
        Assert.Single(fixture.Repositories.Ledgers);
        var second = await fixture.Handler.HandleAsync(fixture.Command with { IdempotencyKey = "new-key" });
        Assert.NotEqual(first.TransactionId, second.TransactionId);
        Assert.Equal(2, fixture.Repositories.Saves);
        Assert.Equal(2, fixture.Idempotency.Commits);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("receiver")]
    [InlineData("currency")]
    public async Task Reused_key_with_different_details_is_a_conflict(string changed)
    {
        var fixture = new Fixture();
        await fixture.Handler.HandleAsync(fixture.Command);
        var command = changed switch
        {
            "amount" => fixture.Command with { Amount = 900 },
            "receiver" => fixture.Command with { ReceiverWalletId = Guid.NewGuid() },
            _ => fixture.Command with { Currency = "USD" }
        };
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => fixture.Handler.HandleAsync(command));
        Assert.Equal(1, fixture.Repositories.Saves);
    }

    [Fact]
    public async Task Other_owner_cannot_replay_or_spend_and_cannot_lookup_original_result()
    {
        var fixture = new Fixture();
        await fixture.Handler.HandleAsync(fixture.Command);
        var other = new TestOwner(Guid.NewGuid());
        var handler = new TransferMoneyHandler(fixture.Repositories, fixture.Repositories, fixture.Idempotency, other);
        await Assert.ThrowsAsync<WalletAccessDeniedException>(() => handler.HandleAsync(fixture.Command));
        var status = await new GetTransferAttemptHandler(fixture.Idempotency, other).HandleAsync(new("ABC-123"));
        Assert.Equal(TransferAttemptStatus.Unavailable, status.Status);
        Assert.Null(status.Result);
        Assert.Equal(1, fixture.Repositories.Saves);
    }

    [Fact]
    public async Task Identity_is_required_even_for_replay()
    {
        var fixture = new Fixture();
        var handler = new TransferMoneyHandler(fixture.Repositories, fixture.Repositories, fixture.Idempotency, new TestOwner(Guid.Empty));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => handler.HandleAsync(fixture.Command));
        Assert.Equal(0, fixture.Idempotency.Commits);
    }

    [Fact]
    public async Task Failed_operation_has_no_replayable_result()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Handler.HandleAsync(fixture.Command with { Amount = 9999 }));
        var lookup = await new GetTransferAttemptHandler(fixture.Idempotency, new TestOwner(fixture.Sender.OwnerId))
            .HandleAsync(new("ABC-123"));
        Assert.Equal(TransferAttemptStatus.Unavailable, lookup.Status);
        Assert.Equal(0, fixture.Repositories.Saves);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains\nnewline")]
    public void Invalid_keys_are_rejected(string key) => Assert.Throws<DomainException>(() => TransferRequestIdentity.ValidateKey(key));

    [Fact]
    public void Fingerprint_is_canonical_culture_independent_and_covers_all_fields()
    {
        var command = new Fixture().Command;
        var initial = TransferRequestIdentity.Fingerprint(command, new Money(500m, "BDT"));
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(initial, TransferRequestIdentity.Fingerprint(command, new Money(500.0000m, " bdt ")));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.Equal(64, initial.Length);
        Assert.NotEqual(initial, TransferRequestIdentity.Fingerprint(command with { SenderWalletId = Guid.NewGuid() }, new Money(500, "BDT")));
        Assert.NotEqual(initial, TransferRequestIdentity.Fingerprint(command with { ReceiverWalletId = Guid.NewGuid() }, new Money(500, "BDT")));
        Assert.NotEqual(initial, TransferRequestIdentity.Fingerprint(command, new Money(501, "BDT")));
        Assert.NotEqual(initial, TransferRequestIdentity.Fingerprint(command, new Money(500, "USD")));
    }

    private sealed class Fixture
    {
        public Wallet Sender { get; } = Wallet.Create(Guid.NewGuid(), "BDT");
        public MemoryIdempotencyRepository Idempotency { get; } = new();
        public Repositories Repositories { get; }
        public TransferMoneyCommand Command { get; }
        public TransferMoneyHandler Handler { get; }
        public Fixture()
        {
            Sender.Credit(new Money(2000, "BDT"));
            var receiver = Wallet.Create(Guid.NewGuid(), "BDT");
            Repositories = new(Sender, receiver);
            Command = new(Sender.Id, receiver.Id, 500, "BDT", "ABC-123");
            Handler = new(Repositories, Repositories, Idempotency, new TestOwner(Sender.OwnerId));
        }
    }

    private sealed class Repositories(Wallet sender, Wallet receiver) : IWalletRepository, ILedgerTransactionRepository
    {
        public int Saves { get; private set; }
        public List<LedgerTransaction> Ledgers { get; } = [];
        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == sender.Id ? sender : id == receiver.Id ? receiver : null);
        public Task<Wallet?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return Task.CompletedTask; }
        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default) { Ledgers.Add(transaction); return Task.CompletedTask; }
    }
}
