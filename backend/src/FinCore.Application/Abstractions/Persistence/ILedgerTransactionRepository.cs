using FinCore.Domain.Entities;

namespace FinCore.Application.Abstractions.Persistence;

public interface ILedgerTransactionRepository
{
    // Stages the aggregate and its entries; the caller owns the shared commit.
    Task AddAsync(LedgerTransaction transaction, CancellationToken cancellationToken = default);
}
