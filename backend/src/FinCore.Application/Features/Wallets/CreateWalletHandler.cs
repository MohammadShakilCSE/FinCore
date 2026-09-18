using FinCore.Application.Abstractions.Persistence;
using FinCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Features.Wallets
{
    public sealed class CreateWalletHandler
    {
        private readonly IWalletRepository _walletRepository;

        public CreateWalletHandler(IWalletRepository walletRepository)
        {
            _walletRepository = walletRepository;
        }

        public async Task<CreateWalletResult> HandleAsync(
            CreateWalletCommand command,
            CancellationToken cancellationToken = default)
        {
            var wallet = Wallet.Create(
                command.OwnerId,
                command.Currency);

            await _walletRepository.AddAsync(
                wallet,
                cancellationToken);

            return new CreateWalletResult(
                wallet.Id,
                wallet.OwnerId,
                wallet.Currency,
                wallet.Balance.Amount,
                wallet.Status.ToString(),
                wallet.CreatedAt);
        }
    }
}
