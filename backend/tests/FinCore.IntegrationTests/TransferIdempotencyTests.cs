using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Application.Features.Transfers.GetTransferAttempt;
using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Application.Idempotency;
using FinCore.Domain.Entities;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;
using FinCore.Infrastructure.Identity;
using FinCore.Infrastructure.Persistence.Context;
using FinCore.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Npgsql;

namespace FinCore.IntegrationTests;

public sealed class TransferIdempotencyTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private async Task<(Wallet Sender, Wallet Receiver, TransferMoneyCommand Command)> SeedAsync(Guid? owner = null, decimal balance = 2000)
    {
        var sender = Wallet.Create(owner ?? Guid.NewGuid(), "BDT");
        if (balance > 0) sender.Credit(new Money(balance, "BDT"));
        var receiver = Wallet.Create(Guid.NewGuid(), "BDT");
        await using var context = database.CreateContext();
        context.Wallets.AddRange(sender, receiver);
        await context.SaveChangesAsync();
        return (sender, receiver, new(sender.Id, receiver.Id, 500, "BDT", "ABC-123"));
    }

    private static TransferMoneyHandler Handler(FinCoreDbContext context, Guid owner,
        ILedgerTransactionRepository? ledger = null, IWalletRepository? wallets = null)
        => new(wallets ?? new WalletRepository(context), ledger ?? new LedgerTransactionRepository(context),
            new IdempotencyRepository(context), new TestOwner(owner));

    [PostgresFact]
    public async Task Lost_response_replay_and_status_survive_new_context_without_additional_writes()
    {
        var data = await SeedAsync();
        await using (var original = database.CreateContext())
            await Handler(original, data.Sender.OwnerId).HandleAsync(data.Command); // Response deliberately discarded.

        TransferMoneyResult originalResult;
        int originalVersion;
        await using (var inspect = database.CreateContext())
        {
            originalResult = (await inspect.IdempotencyRecords.SingleAsync(item => item.OwnerId == data.Sender.OwnerId)).GetResult();
            originalVersion = (await inspect.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Version;
        }
        // Fresh context + handler: no process-local replay cache or tracked entities are reused.
        await using (var retry = database.CreateContext())
        {
            var result = await Handler(retry, data.Sender.OwnerId).HandleAsync(data.Command with { Amount = 500.0000m, Currency = " bdt " });
            Assert.Equal(originalResult, result);
            var status = await new GetTransferAttemptHandler(new IdempotencyRepository(retry), new TestOwner(data.Sender.OwnerId))
                .HandleAsync(new(data.Command.IdempotencyKey));
            Assert.Equal(TransferAttemptStatus.Completed, status.Status);
            Assert.Equal(originalResult, status.Result);
        }
        await using var verify = database.CreateContext();
        var sender = await verify.Wallets.SingleAsync(item => item.Id == data.Sender.Id);
        Assert.Equal(1500, sender.Balance.Amount);
        Assert.Equal(originalVersion, sender.Version);
        Assert.Equal(500, (await verify.Wallets.SingleAsync(item => item.Id == data.Receiver.Id)).Balance.Amount);
        Assert.Equal(data.Receiver.Version + 1, (await verify.Wallets.SingleAsync(item => item.Id == data.Receiver.Id)).Version);
        Assert.Equal(2, await verify.LedgerEntries.CountAsync(item => item.WalletId == data.Sender.Id || item.WalletId == data.Receiver.Id));
        var record = Assert.Single(await verify.IdempotencyRecords.Where(item => item.OwnerId == data.Sender.OwnerId).ToListAsync());
        Assert.Equal(originalResult.TransactionId, record.TransactionId);
        Assert.NotNull(record.CompletedAt);
    }

    [PostgresTheory]
    [InlineData("amount")]
    [InlineData("receiver")]
    [InlineData("currency")]
    public async Task Same_owner_key_with_different_details_is_rejected_without_writes(string changed)
    {
        var data = await SeedAsync();
        await using (var first = database.CreateContext())
            await Handler(first, data.Sender.OwnerId).HandleAsync(data.Command);
        var command = changed switch
        {
            "amount" => data.Command with { Amount = 900 },
            "receiver" => data.Command with { ReceiverWalletId = Guid.NewGuid() },
            _ => data.Command with { Currency = "USD" }
        };
        await using var retry = database.CreateContext();
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => Handler(retry, data.Sender.OwnerId).HandleAsync(command));
        Assert.Equal(1500, (await retry.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
        Assert.Equal(1, await retry.LedgerEntries.CountAsync(item => item.WalletId == data.Sender.Id));
    }

    [PostgresFact]
    public async Task Other_owner_cannot_spend_or_replay_but_can_use_same_literal_key_for_own_wallet()
    {
        var shakil = await SeedAsync();
        var karim = await SeedAsync();
        TransferMoneyResult first;
        await using (var a = database.CreateContext()) first = await Handler(a, shakil.Sender.OwnerId).HandleAsync(shakil.Command);
        await using (var attacker = database.CreateContext())
        {
            await Assert.ThrowsAsync<WalletAccessDeniedException>(() => Handler(attacker, karim.Sender.OwnerId).HandleAsync(shakil.Command));
            var lookup = await new GetTransferAttemptHandler(new IdempotencyRepository(attacker), new TestOwner(karim.Sender.OwnerId))
                .HandleAsync(new(shakil.Command.IdempotencyKey));
            Assert.Equal(TransferAttemptStatus.Unavailable, lookup.Status);
            Assert.Null(lookup.Result);
            Assert.False(await attacker.IdempotencyRecords.AnyAsync(item => item.OwnerId == karim.Sender.OwnerId));
        }
        await using var own = database.CreateContext();
        var second = await Handler(own, karim.Sender.OwnerId).HandleAsync(karim.Command);
        Assert.NotEqual(first.TransactionId, second.TransactionId);
        Assert.Equal(1500, (await own.Wallets.SingleAsync(item => item.Id == karim.Sender.Id)).Balance.Amount);
    }

    [PostgresFact]
    public async Task New_key_with_same_details_is_a_new_transfer()
    {
        var data = await SeedAsync();
        TransferMoneyResult first;
        await using (var a = database.CreateContext()) first = await Handler(a, data.Sender.OwnerId).HandleAsync(data.Command);
        await using (var b = database.CreateContext())
        {
            var second = await Handler(b, data.Sender.OwnerId).HandleAsync(data.Command with { IdempotencyKey = "second-intent" });
            Assert.NotEqual(first.TransactionId, second.TransactionId);
        }
        await using var verify = database.CreateContext();
        Assert.Equal(1000, (await verify.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
        Assert.Equal(2, await verify.IdempotencyRecords.CountAsync(item => item.OwnerId == data.Sender.OwnerId));
        Assert.Equal(2, await verify.LedgerEntries.CountAsync(item => item.WalletId == data.Sender.Id));
    }

    [PostgresFact]
    public async Task Simultaneous_duplicate_waits_then_reports_processing_without_reaching_wallet_mutations()
    {
        var data = await SeedAsync();
        await using var original = database.CreateContext();
        var gate = new HoldingLedger(new LedgerTransactionRepository(original));
        var winner = Handler(original, data.Sender.OwnerId, gate).HandleAsync(data.Command);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var duplicate = database.CreateContext();
            var walletCalls = new CountingWallets(new WalletRepository(duplicate));
            await Assert.ThrowsAsync<IdempotencyInProgressException>(() => Handler(duplicate, data.Sender.OwnerId, wallets: walletCalls).HandleAsync(data.Command));
            Assert.Equal(0, walletCalls.TrackedLoads);
            Assert.Equal(0, walletCalls.Saves);
            await using var lookupContext = database.CreateContext();
            var status = await new GetTransferAttemptHandler(new IdempotencyRepository(lookupContext), new TestOwner(data.Sender.OwnerId))
                .HandleAsync(new(data.Command.IdempotencyKey));
            Assert.Equal(TransferAttemptStatus.Processing, status.Status);
            Assert.Null(status.Result);
        }
        finally { gate.Release.TrySetResult(); }
        var first = await winner;
        await using var retry = database.CreateContext();
        Assert.Equal(first, await Handler(retry, data.Sender.OwnerId).HandleAsync(data.Command));
        Assert.Equal(1500, (await retry.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
        Assert.Equal(1, await retry.IdempotencyRecords.CountAsync(item => item.OwnerId == data.Sender.OwnerId));
        Assert.Equal(1, await retry.LedgerEntries.CountAsync(item => item.WalletId == data.Sender.Id));
    }

    [PostgresFact]
    public async Task Failed_transfer_rolls_back_claim_and_same_key_can_be_retried()
    {
        var data = await SeedAsync(balance: 0);
        await using (var failed = database.CreateContext())
            await Assert.ThrowsAsync<DomainException>(() => Handler(failed, data.Sender.OwnerId).HandleAsync(data.Command));
        await using (var inspect = database.CreateContext())
        {
            Assert.False(await inspect.IdempotencyRecords.AnyAsync(item => item.OwnerId == data.Sender.OwnerId));
            Assert.False(await inspect.LedgerEntries.AnyAsync(item => item.WalletId == data.Sender.Id));
            var sender = await inspect.Wallets.SingleAsync(item => item.Id == data.Sender.Id);
            sender.Credit(new Money(1000, "BDT"));
            await inspect.SaveChangesAsync();
        }
        await using var retry = database.CreateContext();
        await Handler(retry, data.Sender.OwnerId).HandleAsync(data.Command);
        Assert.Equal(500, (await retry.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
    }

    [PostgresFact]
    public async Task Stale_transfer_rolls_back_ledger_receiver_and_completed_record()
    {
        var data = await SeedAsync();
        await using var stale = database.CreateContext();
        await new WalletRepository(stale).GetTrackedByIdAsync(data.Sender.Id);
        await using (var winner = database.CreateContext())
        {
            var sender = await winner.Wallets.SingleAsync(item => item.Id == data.Sender.Id);
            sender.Debit(new Money(100, "BDT"));
            await winner.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Handler(stale, data.Sender.OwnerId).HandleAsync(data.Command));
        Assert.Empty(stale.ChangeTracker.Entries());
        await using var verify = database.CreateContext();
        Assert.False(await verify.IdempotencyRecords.AnyAsync(item => item.OwnerId == data.Sender.OwnerId));
        Assert.False(await verify.LedgerEntries.AnyAsync(item => item.WalletId == data.Sender.Id || item.WalletId == data.Receiver.Id));
        Assert.Equal(1900, (await verify.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
        Assert.Equal(0, (await verify.Wallets.SingleAsync(item => item.Id == data.Receiver.Id)).Balance.Amount);
    }

    [PostgresFact]
    public async Task Abandoned_claim_rolls_back_and_status_probe_does_not_create_a_record()
    {
        var data = await SeedAsync();
        await using (var abandoned = database.CreateContext())
        {
            await using var claim = await new IdempotencyRepository(abandoned).ClaimAsync(data.Sender.OwnerId,
                TransferRequestIdentity.Operation, data.Command.IdempotencyKey,
                TransferRequestIdentity.Fingerprint(data.Command, new Money(500, "BDT")));
            Assert.True(claim.IsNew);
            // Dispose without completion, as with a failed/disconnected operation.
        }
        await using var fresh = database.CreateContext();
        var status = await new GetTransferAttemptHandler(new IdempotencyRepository(fresh), new TestOwner(data.Sender.OwnerId))
            .HandleAsync(new(data.Command.IdempotencyKey));
        Assert.Equal(TransferAttemptStatus.Unavailable, status.Status);
        Assert.False(await fresh.IdempotencyRecords.AnyAsync(item => item.OwnerId == data.Sender.OwnerId));
        await Handler(fresh, data.Sender.OwnerId).HandleAsync(data.Command);
    }

    [PostgresFact]
    public async Task Scoped_unique_index_enforces_uniqueness_and_unrelated_errors_propagate()
    {
        var data = await SeedAsync();
        await using var context = database.CreateContext();
        await Handler(context, data.Sender.OwnerId).HandleAsync(data.Command);
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(data.Sender.OwnerId, TransferRequestIdentity.Operation, "ABC-123", new string('A', 64)));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("UX_IdempotencyRecords_Owner_Operation_Key", postgres.ConstraintName);

        await using var other = database.CreateContext();
        // Not a duplicate-key collision: must remain a database error, not a replay or conflict result.
        var lengthError = await Assert.ThrowsAsync<PostgresException>(() => new IdempotencyRepository(other)
            .ClaimAsync(data.Sender.OwnerId, new string('x', 65), "another-key", new string('A', 64)));
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, lengthError.SqlState);
    }

    [PostgresFact]
    public async Task Default_identity_refuses_transfer_instead_of_inventing_an_owner()
    {
        var data = await SeedAsync();
        await using var context = database.CreateContext();
        var handler = new TransferMoneyHandler(new WalletRepository(context), new LedgerTransactionRepository(context),
            new IdempotencyRepository(context), new UnauthenticatedOwner());
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => handler.HandleAsync(data.Command));
        Assert.False(await context.IdempotencyRecords.AnyAsync(item => item.OwnerId == data.Sender.OwnerId));
    }

    [PostgresFact]
    public async Task Both_see_absent_key_but_unique_claim_loser_rechecks_and_replays_winner()
    {
        var data = await SeedAsync();
        await using var original = database.CreateContext();
        var gate = new HoldingLedger(new LedgerTransactionRepository(original));
        var winner = Handler(original, data.Sender.OwnerId, gate).HandleAsync(data.Command);
        var claimGate = new ClaimInsertGate();
        await using var competing = new FinCoreDbContext(new DbContextOptionsBuilder<FinCoreDbContext>()
            .UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(claimGate).Options);
        Task<TransferMoneyResult>? duplicate = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            duplicate = Handler(competing, data.Sender.OwnerId).HandleAsync(data.Command);
            // Competitor already found no committed record, but pause its INSERT at the driver boundary.
            await claimGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            gate.Release.TrySetResult();
            var first = await winner;
            claimGate.Release.TrySetResult();
            Assert.Equal(first, await duplicate);
        }
        finally
        {
            gate.Release.TrySetResult();
            claimGate.Release.TrySetResult();
            await winner;
            if (duplicate is not null) await duplicate;
        }
        await using var verify = database.CreateContext();
        Assert.Equal(1500, (await verify.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
        Assert.Single(await verify.IdempotencyRecords.Where(item => item.OwnerId == data.Sender.OwnerId).ToListAsync());
        Assert.Equal(1, await verify.LedgerEntries.CountAsync(item => item.WalletId == data.Sender.Id));
    }

    [PostgresFact]
    public async Task Unrelated_ledger_unique_error_is_not_replayed_and_rolls_back_financial_changes()
    {
        var original = await SeedAsync();
        TransferMoneyResult prior;
        await using (var first = database.CreateContext()) prior = await Handler(first, original.Sender.OwnerId).HandleAsync(original.Command);
        var data = await SeedAsync();
        await using (var failing = database.CreateContext())
        {
            var ledger = new DuplicateReferenceLedger(failing, prior.Reference);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => Handler(failing, data.Sender.OwnerId, ledger).HandleAsync(data.Command));
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal("IX_LedgerTransactions_Reference", postgres.ConstraintName);
        }
        await using var verify = database.CreateContext();
        Assert.Equal(2000, (await verify.Wallets.SingleAsync(item => item.Id == data.Sender.Id)).Balance.Amount);
        Assert.Equal(0, (await verify.Wallets.SingleAsync(item => item.Id == data.Receiver.Id)).Balance.Amount);
        Assert.False(await verify.IdempotencyRecords.AnyAsync(item => item.OwnerId == data.Sender.OwnerId));
        Assert.False(await verify.LedgerEntries.AnyAsync(item => item.WalletId == data.Sender.Id));
    }

    private sealed class DuplicateReferenceLedger(FinCoreDbContext context, string reference) : ILedgerTransactionRepository
    {
        public async Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default)
        {
            await new LedgerTransactionRepository(context).AddAsync(transaction, cancellationToken);
            context.Entry(transaction).Property(item => item.Reference).CurrentValue = reference;
        }
    }

    private sealed class ClaimInsertGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"IdempotencyRecords\"", StringComparison.Ordinal))
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class HoldingLedger(ILedgerTransactionRepository inner) : ILedgerTransactionRepository
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default)
        {
            await inner.AddAsync(transaction, cancellationToken);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CountingWallets(IWalletRepository inner) : IWalletRepository
    {
        public int TrackedLoads { get; private set; }
        public int Saves { get; private set; }
        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<Wallet?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        { TrackedLoads++; return inner.GetTrackedByIdAsync(id, cancellationToken); }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return inner.SaveChangesAsync(cancellationToken); }
        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default) => inner.AddAsync(wallet, cancellationToken);
    }
}
