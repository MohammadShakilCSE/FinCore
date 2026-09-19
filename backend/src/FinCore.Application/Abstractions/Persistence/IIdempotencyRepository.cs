using FinCore.Application.Idempotency;

namespace FinCore.Application.Abstractions.Persistence;

public interface IIdempotencyRepository
{
    Task<IIdempotencyClaim> ClaimAsync(Guid ownerId, string operation, string key, string fingerprint,
        CancellationToken cancellationToken = default);
    // A null result is unavailable; lock contention throws IdempotencyInProgressException.
    Task<IdempotencyRecord?> GetStatusAsync(Guid ownerId, string operation, string key,
        CancellationToken cancellationToken = default);
}

public interface IIdempotencyClaim : IAsyncDisposable
{
    bool IsNew { get; }
    IdempotencyRecord Record { get; }
    // Commit only after the completed record and all financial changes have been saved together.
    Task CommitAsync(CancellationToken cancellationToken = default);
}
