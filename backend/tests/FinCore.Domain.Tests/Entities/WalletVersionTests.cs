using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Domain.Tests.Entities;

public sealed class WalletVersionTests
{
    [Fact]
    public void Create_starts_at_one_and_each_balance_change_increments_once()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        Assert.Equal(1, wallet.Version);
        wallet.Credit(new Money(100, "BDT"));
        Assert.Equal(2, wallet.Version);
        wallet.Debit(new Money(30, "BDT"));
        Assert.Equal(3, wallet.Version);
    }

    [Theory]
    [InlineData("zero-credit")]
    [InlineData("wrong-currency-credit")]
    [InlineData("zero-debit")]
    [InlineData("insufficient-debit")]
    [InlineData("wrong-currency-debit")]
    [InlineData("close-funded")]
    [InlineData("suspended-credit")]
    [InlineData("suspended-debit")]
    [InlineData("suspend-twice")]
    [InlineData("activate-suspended")]
    public void Failed_operation_preserves_version(string operation)
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        wallet.Credit(new Money(100, "BDT"));
        if (operation is "suspended-credit" or "suspended-debit" or "suspend-twice" or "activate-suspended")
            wallet.Suspend();
        var before = wallet.Version;
        Action action = operation switch
        {
            "zero-credit" => () => wallet.Credit(new Money(0, "BDT")),
            "wrong-currency-credit" => () => wallet.Credit(new Money(1, "USD")),
            "zero-debit" => () => wallet.Debit(new Money(0, "BDT")),
            "insufficient-debit" => () => wallet.Debit(new Money(101, "BDT")),
            "wrong-currency-debit" => () => wallet.Debit(new Money(1, "USD")),
            "close-funded" => wallet.Close,
            "suspended-credit" => () => wallet.Credit(new Money(1, "BDT")),
            "suspended-debit" => () => wallet.Debit(new Money(1, "BDT")),
            "suspend-twice" => wallet.Suspend,
            _ => wallet.Activate
        };
        Assert.Throws<DomainException>(action);
        Assert.Equal(before, wallet.Version);
    }

    [Fact]
    public void Status_changes_increment_but_no_ops_do_not()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        wallet.Activate();
        Assert.Equal(1, wallet.Version);
        wallet.Suspend();
        Assert.Equal(2, wallet.Version);
        wallet.Close(); // Current Domain represents closed as Suspended.
        Assert.Equal(2, wallet.Version);
        var other = Wallet.Create(Guid.NewGuid(), "BDT");
        other.Close();
        Assert.Equal(2, other.Version);
        other.Close();
        Assert.Equal(2, other.Version);
    }

    [Fact]
    public void Activating_an_existing_inactive_wallet_increments_once()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), "BDT");
        // Simulate persisted Inactive state: no current public method creates that state.
        typeof(Wallet).GetProperty(nameof(Wallet.Status))!.SetValue(wallet, WalletStatus.Inactive);
        wallet.Activate();
        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Equal(2, wallet.Version);
    }
}
