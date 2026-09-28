using System.Data;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Application.Idempotency;
using FinCore.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FinCore.Infrastructure.Persistence.Repositories;

public sealed class IdempotencyRepository(FinCoreDbContext context) : IIdempotencyRepository
{
    private Task<IdempotencyRecord?> FindAsync(Guid ownerId, string operation, string key, CancellationToken cancellationToken)
        => context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            record => record.OwnerId == ownerId && record.Operation == operation && record.IdempotencyKey == key, cancellationToken);

    public async Task<IIdempotencyClaim> ClaimAsync(Guid ownerId, string operation, string key, string fingerprint,CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("An idempotency attempt must own its transaction.");

        var existing = await FindAsync(ownerId, operation, key, cancellationToken);
        if (existing is not null) return new Claim(context, existing, null);

        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var candidate = IdempotencyRecord.Create(ownerId, operation, key, fingerprint);
            // Only key acquisition is bounded. A duplicate never reaches wallet mutations.
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '2s'", cancellationToken);
            var inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "IdempotencyRecords"
                    ("Id", "OwnerId", "Operation", "IdempotencyKey", "RequestFingerprint", "Status", "CreatedAt")
                VALUES ({candidate.Id}, {ownerId}, {operation}, {key}, {fingerprint}, 'Processing', {candidate.CreatedAt})
                ON CONFLICT ("OwnerId", "Operation", "IdempotencyKey") DO NOTHING
                """, cancellationToken);
            await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '0'", cancellationToken);
            if (inserted == 0)
            {
                await transaction.DisposeAsync();
                // Separate statement/snapshot sees the now-committed competing record.
                existing = await FindAsync(ownerId, operation, key, cancellationToken)
                    ?? throw new IdempotencyInProgressException();
                return new Claim(context, existing, null);
            }

            // The raw INSERT is uncommitted. Load it as tracked so completion shares the final save.
            var record = await context.IdempotencyRecords.SingleAsync(item => item.Id == candidate.Id, cancellationToken);
            return new Claim(context, record, transaction);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            await transaction.DisposeAsync();
            context.ChangeTracker.Clear();
            throw new IdempotencyInProgressException(exception);
        }
        catch
        {
            await transaction.DisposeAsync();
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<IdempotencyRecord?> GetStatusAsync(Guid ownerId, string operation, string key,
        CancellationToken cancellationToken = default)
    {
        // An uncommitted Processing row is invisible to SELECT. Probe the unique key under
        // a transaction, then always roll back a newly acquired probe: status lookup never commits.
        await using var probe = await ClaimAsync(ownerId, operation, key, new string('0', 64), cancellationToken);
        return probe.IsNew ? null : probe.Record;
    }

    private sealed class Claim(FinCoreDbContext context, IdempotencyRecord record, IDbContextTransaction? transaction) : IIdempotencyClaim
    {
        private bool _committed;
        public bool IsNew => transaction is not null;
        public IdempotencyRecord Record => record;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (transaction is null || record.Status != "Completed" || context.Entry(record).State != EntityState.Unchanged)
                throw new InvalidOperationException("Save the completed attempt and financial changes before committing.");
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (transaction is null) return;
            try { await transaction.DisposeAsync(); }
            finally { if (!_committed) context.ChangeTracker.Clear(); }
        }
    }
}
