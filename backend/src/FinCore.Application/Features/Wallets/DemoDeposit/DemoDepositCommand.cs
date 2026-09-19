namespace FinCore.Application.Features.Wallets.DemoDeposit;

public sealed record DemoDepositCommand(Guid WalletId, decimal Amount, string Currency);
