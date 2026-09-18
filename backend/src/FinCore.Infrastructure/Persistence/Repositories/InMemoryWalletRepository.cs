using FinCore.Application.Abstractions.Persistence;
using FinCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Persistence.Repositories
{
    public sealed class InMemoryWalletRepository
     : IWalletRepository
    {
        private readonly List<Wallet> _wallets = [];

        public Task AddAsync(
            Wallet wallet,
            CancellationToken cancellationToken = default)
        {
            _wallets.Add(wallet);

            return Task.CompletedTask;
        }
    }
}
