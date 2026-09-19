namespace FinCore.Application.Features.Transfers.TransferMoney;

public sealed record TransferMoneyCommand(
    Guid SenderWalletId, Guid ReceiverWalletId, decimal Amount, string Currency, string IdempotencyKey);
