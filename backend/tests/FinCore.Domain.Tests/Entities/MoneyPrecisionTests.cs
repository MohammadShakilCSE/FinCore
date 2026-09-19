using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Domain.Tests.Entities;

public sealed class MoneyPrecisionTests
{
    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U1D")]
    public void Rejects_invalid_currency_codes(string currency)
        => Assert.Throws<DomainException>(() => new Money(0, currency));

    [Fact]
    public void Rejects_amount_that_would_be_rounded()
        => Assert.Throws<DomainException>(() => new Money(0.00001m, "USD"));

    [Fact]
    public void Rejects_amount_outside_supported_range()
        => Assert.Throws<DomainException>(() => new Money(1_000_000_000_000_000m, "USD"));

    [Fact]
    public void Accepts_maximum_amount_and_insignificant_trailing_zeroes()
    {
        Assert.Equal(999_999_999_999_999.9999m, new Money(999_999_999_999_999.9999m, "USD").Amount);
        Assert.Equal(1.1234m, new Money(1.12340m, "USD").Amount);
    }
}
