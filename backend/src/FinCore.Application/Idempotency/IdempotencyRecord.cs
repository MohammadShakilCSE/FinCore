using System.Text.Json;
using FinCore.Application.Features.Transfers.TransferMoney;

namespace FinCore.Application.Idempotency;

// Request-processing data, not a financial Domain aggregate.
public sealed class IdempotencyRecord
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Operation { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string RequestFingerprint { get; private set; } = "";
    public string Status { get; private set; } = "Processing";
    public Guid? TransactionId { get; private set; }
    public string? ResponsePayload { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord Create(Guid ownerId, string operation, string key, string fingerprint)
        => new() { Id = Guid.NewGuid(), OwnerId = ownerId, Operation = operation,
            IdempotencyKey = key, RequestFingerprint = fingerprint, CreatedAt = DateTime.UtcNow };

    public void Complete(TransferMoneyResult result)
    {
        if (Status != "Processing") throw new InvalidOperationException("Attempt is already completed.");
        TransactionId = result.TransactionId;
        ResponsePayload = JsonSerializer.Serialize(result);
        CompletedAt = DateTime.UtcNow;
        Status = "Completed";
    }

    public TransferMoneyResult GetResult()
    {
        if (Status != "Completed" || ResponsePayload is null)
            throw new InvalidOperationException("Attempt has no completed result.");
        return JsonSerializer.Deserialize<TransferMoneyResult>(ResponsePayload)
            ?? throw new InvalidOperationException("Stored result is invalid.");
    }
}
