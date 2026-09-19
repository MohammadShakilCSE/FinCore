
using FinCore.Domain.Exceptions;

namespace FinCore.Domain.ValueObjects;
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative.");

        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency is required.");

        Amount = amount;
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || normalizedCurrency.Any(character => character < 'A' || character > 'Z'))
            throw new DomainException("Currency must contain exactly three letters.");

        // FinCore currently supports up to four fractional digits, without silent rounding.
        if (amount > 999_999_999_999_999.9999m || decimal.Round(amount, 4) != amount)
            throw new DomainException("Amount must fit 15 whole digits and at most four decimal places.");

        Currency = normalizedCurrency;
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        if (Amount < other.Amount)
            throw new DomainException("Insufficient funds.");

        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Cannot operate on amounts with different currencies.");
    }
}
