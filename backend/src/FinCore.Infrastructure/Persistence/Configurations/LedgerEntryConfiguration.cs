using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_LedgerEntries_Amount", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_LedgerEntries_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
        });
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.LedgerTransactionId).IsRequired();
        builder.HasIndex(entry => entry.LedgerTransactionId);
        builder.Property(entry => entry.WalletId).IsRequired();
        builder.Property(entry => entry.EntryType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entry => entry.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.OwnsOne(entry => entry.Amount, amount =>
        {
            amount.Property(money => money.Amount).HasColumnName("Amount").HasPrecision(19, 4).IsRequired();
            amount.Property(money => money.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });
        builder.Navigation(entry => entry.Amount).IsRequired();
    }
}
