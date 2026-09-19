namespace FinCore.Application.Features.Wallets.DemoDeposit;

public sealed record DemoDepositResult(
    Guid WalletId,
    decimal DepositedAmount,
    decimal CurrentBalance,
    string Currency);
