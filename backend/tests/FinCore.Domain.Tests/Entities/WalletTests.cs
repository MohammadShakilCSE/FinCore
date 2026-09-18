using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Domain.Exceptions;
using FinCore.valueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Tests.Entities
{
    public class WalletTests
    {
        [Fact]
        public void Create_Should_Create_Wallet_With_Zero_Balance()
        {
            var ownerId = Guid.NewGuid();

            var wallet = Wallet.Create(
                ownerId,
                "BDT");

            Assert.Equal(0m, wallet.Balance.Amount);
        }


        [Fact]
        public void Create_Should_Create_Active_Wallet()
        {
            var wallet = Wallet.Create(
                Guid.NewGuid(),
                "BDT");

            Assert.Equal(
                WalletStatus.Active,
                wallet.Status);
        }


        [Fact]
        public void Create_Should_Normalize_Currency()
        {
            var wallet = Wallet.Create(
                Guid.NewGuid(),
                "bdt");

            Assert.Equal(
                "BDT",
                wallet.Currency);
        }

        [Fact]
        public void Create_Should_Throw_When_OwnerId_Is_Empty()
        {
            Assert.Throws<DomainException>(() =>
                Wallet.Create(
                    Guid.Empty,
                    "BDT"));
        }

        [Fact]
        public void Credit_Should_Increase_Balance()
        {
            var wallet = Wallet.Create(
                Guid.NewGuid(),
                "BDT");

            wallet.Credit(
                new Money(1000m, "BDT"));

            Assert.Equal(
                1000m,
                wallet.Balance.Amount);
        }

        [Fact]
        public void Debit_Should_Decrease_Balance()
        {
            var wallet = Wallet.Create(
                Guid.NewGuid(),
                "BDT");

            wallet.Credit(
                new Money(1000m, "BDT"));

            wallet.Debit(
                new Money(300m, "BDT"));

            Assert.Equal(
                700m,
                wallet.Balance.Amount);
        }


        [Fact]
        public void Debit_Should_Throw_When_Balance_Is_Insufficient()
        {
            var wallet = Wallet.Create(
                Guid.NewGuid(),
                "BDT");

            wallet.Credit(
                new Money(500m, "BDT"));

            Assert.Throws<DomainException>(() =>
                wallet.Debit(
                    new Money(1000m, "BDT")));
        }


        [Fact]
        public void Credit_Should_Throw_When_Currency_Does_Not_Match()
        {
            var wallet = Wallet.Create(
                Guid.NewGuid(),
                "BDT");

            Assert.Throws<DomainException>(() =>
                wallet.Credit(
                    new Money(100m, "USD")));
        }
    }
}
