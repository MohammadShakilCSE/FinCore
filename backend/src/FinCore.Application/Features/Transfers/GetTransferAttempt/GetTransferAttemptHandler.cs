using FinCore.Application.Abstractions.Identity;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Application.Idempotency;

namespace FinCore.Application.Features.Transfers.GetTransferAttempt;

public sealed record GetTransferAttemptQuery(string IdempotencyKey);
public enum TransferAttemptStatus { Unavailable, Processing, Completed }
public sealed record TransferAttemptResult(TransferAttemptStatus Status, TransferMoneyResult? Result = null);

public sealed class GetTransferAttemptHandler(IIdempotencyRepository repository, ICurrentOwner currentOwner)
{
    public async Task<TransferAttemptResult> HandleAsync(GetTransferAttemptQuery query, CancellationToken cancellationToken = default)
    {
        var owner = currentOwner.GetRequiredOwnerId();
        if (owner == Guid.Empty) throw new AuthenticationRequiredException();
        TransferRequestIdentity.ValidateKey(query.IdempotencyKey);
        try
        {
            var record = await repository.GetStatusAsync(owner, TransferRequestIdentity.Operation, query.IdempotencyKey, cancellationToken);
            return record is null ? new(TransferAttemptStatus.Unavailable)
                : record.Status == "Completed" ? new(TransferAttemptStatus.Completed, record.GetResult())
                : new(TransferAttemptStatus.Processing);
        }
        catch (IdempotencyInProgressException)
        {
            return new(TransferAttemptStatus.Processing);
        }
    }
}
