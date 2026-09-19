using FinCore.Domain.Common;
using FinCore.Domain.Enums;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Domain.Entities;

public sealed class LedgerTransaction : AggregateRoot
{
    private readonly List<LedgerEntry> _entries = new();
    public string Reference { get; private set; } = string.Empty;
    public LedgerTransactionStatus Status { get; private set; }

    public IReadOnlyCollection<LedgerEntry> Entries =>_entries.AsReadOnly();
    private LedgerTransaction() { }

    public static LedgerTransaction CreateTransfer(
        Guid senderWalletId,
        Guid receiverWalletId,
        Money amount)
    {
        if (senderWalletId == Guid.Empty ||
            receiverWalletId == Guid.Empty)
        {
            throw new DomainException(
                "Both wallets are required.");
        }

        if (senderWalletId == receiverWalletId)
        {
            throw new DomainException(
                "Cannot transfer to the same wallet.");
        }

        if (amount is null || amount.Amount <= 0)
        {
            throw new DomainException(
                "Transfer amount must be positive.");
        }

        var transaction = new LedgerTransaction
        {
            Id = Guid.NewGuid(),
            Reference = Guid.NewGuid().ToString("N"),
            Status = LedgerTransactionStatus.Draft,
        };

        transaction.AddEntry(senderWalletId,LedgerEntryType.Debit, amount);

        transaction.AddEntry(receiverWalletId,LedgerEntryType.Credit,amount);

        transaction.Post();

        return transaction;
    }

    private void AddEntry( Guid walletId,LedgerEntryType entryType, Money amount)
    {
        if (Status != LedgerTransactionStatus.Draft)
        {
            throw new DomainException(
                "Posted transactions cannot be modified.");
        }

        var entry = LedgerEntry.Create(
            Id,
            walletId,
            entryType,
            amount);

        _entries.Add(entry);
    }

    private void Post()
    {
        if (Status != LedgerTransactionStatus.Draft)
        {
            throw new DomainException(
                "Transaction has already been posted.");
        }

        if (_entries.Count < 2)
        {
            throw new DomainException(
                "A transaction requires at least two entries.");
        }

        var currency = _entries[0].Amount.Currency;

        if (_entries.Any(x => x.Amount.Currency != currency))
        {
            throw new DomainException(
                "Ledger currencies do not match.");
        }

        var totalDebit = _entries
            .Where(x => x.EntryType == LedgerEntryType.Debit)
            .Sum(x => x.Amount.Amount);

        var totalCredit = _entries
            .Where(x => x.EntryType == LedgerEntryType.Credit)
            .Sum(x => x.Amount.Amount);

        if (totalDebit != totalCredit)
        {
            throw new DomainException(
                "Ledger transaction is not balanced.");
        }

        Status = LedgerTransactionStatus.Posted;
    }
}