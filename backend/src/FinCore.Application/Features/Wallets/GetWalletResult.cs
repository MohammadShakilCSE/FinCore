namespace FinCore.Application.Features.Wallets;

public sealed record GetWalletResult(Guid WalletId, Guid OwnerId, string Currency,
    decimal Balance, string Status, DateTime CreatedAt);
