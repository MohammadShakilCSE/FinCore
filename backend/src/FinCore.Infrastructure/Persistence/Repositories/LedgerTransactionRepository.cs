using FinCore.Application.Abstractions.Persistence;
using FinCore.Domain.Entities;
using FinCore.Infrastructure.Persistence.Context;

namespace FinCore.Infrastructure.Persistence.Repositories;

public sealed class LedgerTransactionRepository(FinCoreDbContext context) : ILedgerTransactionRepository
{
    public async Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default)
        => await context.LedgerTransactions.AddAsync(transaction, cancellationToken);
}
