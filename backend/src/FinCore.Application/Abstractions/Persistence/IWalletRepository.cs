using FinCore.Domain.Entities;

namespace FinCore.Application.Abstractions.Persistence
{
    public interface IWalletRepository
    {
        // Completes only after the wallet has been persisted.
        Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);

        // Returns a read-only snapshot; changes to the result are not automatically persisted.
        Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        // Loads a wallet whose domain changes can be persisted by SaveChangesAsync.
        Task<Wallet?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
