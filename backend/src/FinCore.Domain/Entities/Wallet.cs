namespace FinCore.Domain.Entities;
using FinCore.Domain.Common;
using FinCore.Domain.Enums;
using FinCore.valueObjects;
using FinCore.Domain.Exceptions;

public sealed class Wallet : AggregateRoot
{
    public Guid OwnerId { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Money Balance { get; private set; } = null!; 
    public WalletStatus Status { get; private set; }
    private Wallet()
    {
    }

    public static Wallet Create(Guid ownerId, string currency)
    {
        if (ownerId == Guid.Empty)
            throw new DomainException("OwnerId is required.");

        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency is required.");


        var normalizedCurrency =
            currency.Trim().ToUpperInvariant();

        var wallet = new Wallet
        {
            OwnerId = ownerId,
            Currency = normalizedCurrency,
            Balance = new Money(0, normalizedCurrency),
            Status = WalletStatus.Active
        };

        return wallet;
    }
     public void Credit(Money money)
    {
        EnsureActive();
        EnsureSameCurrency(money);

        if (money.Amount <= 0)
            throw new DomainException(
                "Credit amount must be greater than zero.");

        Balance = Balance.Add(money);
    }

    public void Debit(Money money)
    {
        EnsureActive();
        EnsureSameCurrency(money);

        if (money.Amount <= 0)
            throw new DomainException(
                "Debit amount must be greater than zero.");

        if (Balance.Amount < money.Amount)
            throw new DomainException(
                "Insufficient wallet balance.");

        Balance = Balance.Subtract(money);
    }

  public void Suspend()
    {
        if (Status == WalletStatus.Suspended)
            throw new DomainException(
                "Wallet is already suspended.");

        Status = WalletStatus.Suspended;
    }

 public void Activate()
    {
        if (Status == WalletStatus.Suspended)
            throw new DomainException(
                "Closed wallet cannot be activated.");

        Status = WalletStatus.Active;
    }

    public void Close()
    {
        if (Balance.Amount != 0)
            throw new DomainException(
                "Wallet must have zero balance before closing.");

        Status = WalletStatus.Suspended;
    }

    private void EnsureActive()
    {
        if (Status != WalletStatus.Active)
            throw new DomainException(
                "Wallet is not active.");
    }

    private void EnsureSameCurrency(
        Money money)
    {
        if (money.Currency != Currency)
            throw new DomainException(
                "Currency does not match wallet currency.");
    }
    
}