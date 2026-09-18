using FinCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Abstractions.Persistence
{
    public interface IWalletRepository
    {
        Task AddAsync( Wallet wallet, CancellationToken cancellationToken = default);
    }
}
