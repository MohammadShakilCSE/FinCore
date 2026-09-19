using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", table =>
        {
            table.HasCheckConstraint("CK_Wallets_OwnerId", "\"OwnerId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Wallets_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("CK_Wallets_Balance", "\"Balance\" >= 0");
            table.HasCheckConstraint("CK_Wallets_BalanceCurrency", "\"BalanceCurrency\" = \"Currency\"");
            table.HasCheckConstraint("CK_Wallets_Status", "\"Status\" IN ('Active', 'Inactive', 'Suspended')");
        });
        builder.HasKey(wallet => wallet.Id);
        builder.Property(wallet => wallet.Id).ValueGeneratedNever();
        builder.Property(wallet => wallet.OwnerId).IsRequired();
        builder.Property(wallet => wallet.Version).IsRequired().IsConcurrencyToken();
        builder.Property(wallet => wallet.Currency).HasMaxLength(3).IsRequired();
        builder.Property(wallet => wallet.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(wallet => wallet.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        // Owned mapping supports Money's existing immutable constructor without EF attributes in Domain.
        builder.OwnsOne(wallet => wallet.Balance, balance =>
        {
            balance.Property(money => money.Amount).HasColumnName("Balance").HasPrecision(19, 4).IsRequired();
            balance.Property(money => money.Currency).HasColumnName("BalanceCurrency").HasMaxLength(3).IsRequired();
        });
        builder.Navigation(wallet => wallet.Balance).IsRequired();
    }
}
