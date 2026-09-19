namespace FinCore.Application.Features.Transfers.TransferMoney;

public sealed record TransferMoneyResult(Guid TransactionId, string Reference, decimal Amount, string Currency);
