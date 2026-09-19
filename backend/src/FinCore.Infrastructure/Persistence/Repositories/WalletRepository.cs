using FinCore.Application.Abstractions.Persistence;
using FinCore.Domain.Entities;
using FinCore.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Persistence.Repositories;

public sealed class WalletRepository(FinCoreDbContext context) : IWalletRepository
{
    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Wallets.AsNoTracking().SingleOrDefaultAsync(wallet => wallet.Id == id, cancellationToken);
}
