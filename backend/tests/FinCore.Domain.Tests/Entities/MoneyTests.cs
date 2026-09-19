using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Tests.Entities
{
    public   class MoneyTests
    {
        [Fact]
        public void Constructor_Should_Normalize_Currency()
        {
            var money = new Money(
                1000m,
                "bdt");

            Assert.Equal(
                "BDT",
                money.Currency);
        }

        [Fact]
        public void Constructor_Should_Throw_When_Amount_Is_Negative()
        {
            Assert.Throws<DomainException>(() =>
                new Money(
                    -100m,
                    "BDT"));
        }

        [Fact]
        public void Add_Should_Add_Same_Currency()
        {
            var first = new Money(
                1000m,
                "BDT");

            var second = new Money(
                500m,
                "BDT");

            var result = first.Add(second);

            Assert.Equal(
                1500m,
                result.Amount);
        }

        [Fact]
        public void Add_Should_Throw_For_Different_Currencies()
        {
            var bdt = new Money(
                1000m,
                "BDT");

            var usd = new Money(
                100m,
                "USD");

            Assert.Throws<DomainException>(() =>
                bdt.Add(usd));
        }
    }
}
