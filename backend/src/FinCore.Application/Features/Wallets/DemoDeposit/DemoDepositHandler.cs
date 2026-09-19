using FinCore.Application.Abstractions.Persistence;
using FinCore.valueObjects;

namespace FinCore.Application.Features.Wallets.DemoDeposit;

public sealed class DemoDepositHandler(IWalletRepository repository)
{
    public async Task<DemoDepositResult?> HandleAsync(
        DemoDepositCommand command,
        CancellationToken cancellationToken = default)
    {
        var wallet = await repository.GetTrackedByIdAsync(command.WalletId, cancellationToken);
        if (wallet is null) return null;

        var money = new Money(command.Amount, command.Currency);
        wallet.Credit(money);
        await repository.SaveChangesAsync(cancellationToken);

        return new DemoDepositResult(wallet.Id, money.Amount, wallet.Balance.Amount, wallet.Currency);
    }
}
