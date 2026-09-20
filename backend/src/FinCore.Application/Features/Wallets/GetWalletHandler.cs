using FinCore.Application.Abstractions.Persistence;

namespace FinCore.Application.Features.Wallets;

public sealed class GetWalletHandler(IWalletRepository repository, FinCore.Application.Abstractions.Authentication.ICurrentUser currentUser)
{
    public async Task<GetWalletResult?> HandleAsync(GetWalletQuery query, CancellationToken cancellationToken = default)
    {
        var ownerId = currentUser.UserId;
        var wallet = await repository.GetByIdAsync(query.WalletId, cancellationToken);
        return wallet is null || wallet.OwnerId != ownerId ? null : new GetWalletResult(wallet.Id, wallet.OwnerId,
            wallet.Currency, wallet.Balance.Amount, wallet.Status.ToString(), wallet.CreatedAt);
    }
}
