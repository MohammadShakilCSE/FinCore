using FinCore.Application.Abstractions.Identity;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Idempotency;

namespace FinCore.Application.Tests;

internal sealed class TestOwner(Guid id) : ICurrentOwner
{
    public Guid GetRequiredOwnerId() => id;
}

// Unit-test double only; database race behavior is verified separately against PostgreSQL.
internal sealed class MemoryIdempotencyRepository : IIdempotencyRepository
{
    private readonly Dictionary<(Guid, string, string), IdempotencyRecord> _records = [];
    public int Commits { get; private set; }
    public Task<IIdempotencyClaim> ClaimAsync(Guid ownerId, string operation, string key, string fingerprint, CancellationToken cancellationToken = default)
    {
        var scope = (ownerId, operation, key);
        var existing = _records.GetValueOrDefault(scope);
        return Task.FromResult<IIdempotencyClaim>(new Claim(existing ?? IdempotencyRecord.Create(ownerId, operation, key, fingerprint), existing is null,
            record => { _records.Add(scope, record); Commits++; }));
    }
    public Task<IdempotencyRecord?> GetStatusAsync(Guid ownerId, string operation, string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.GetValueOrDefault((ownerId, operation, key)));
    private sealed class Claim(IdempotencyRecord record, bool isNew, Action<IdempotencyRecord> commit) : IIdempotencyClaim
    {
        public bool IsNew => isNew;
        public IdempotencyRecord Record => record;
        public Task CommitAsync(CancellationToken cancellationToken = default) { commit(record); return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
