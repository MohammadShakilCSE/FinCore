using FinCore.Domain.Common;
using FinCore.Domain.Enums;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;  

namespace FinCore.Domain.Entities;

public sealed class LedgerEntry : Entity
{
    public Guid LedgerTransactionId { get; private set; }

    public Guid WalletId { get; private set; }

    public LedgerEntryType EntryType { get; private set; }

    public Money Amount { get; private set; } = null!;

    private LedgerEntry() { }

    internal static LedgerEntry Create(
        Guid transactionId,
        Guid walletId,
        LedgerEntryType entryType,
        Money amount)
    {
        if (transactionId == Guid.Empty ||
            walletId == Guid.Empty)
        {
            throw new DomainException(
                "Transaction and Wallet are required.");
        }

        if (!Enum.IsDefined(entryType))
            throw new DomainException("Invalid entry type.");

        if (amount is null || amount.Amount <= 0)
            throw new DomainException(
                "Ledger amount must be positive.");

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            LedgerTransactionId = transactionId,
            WalletId = walletId,
            EntryType = entryType,
            // Each entry owns a separate instance with the same monetary value.
            Amount = new Money(amount.Amount, amount.Currency)
        };
    }
}
