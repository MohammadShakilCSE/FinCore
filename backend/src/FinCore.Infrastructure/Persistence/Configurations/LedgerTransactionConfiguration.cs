using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

public sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions");
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedNever();
        builder.Property(transaction => transaction.Reference).HasMaxLength(64).IsRequired();
        builder.HasIndex(transaction => transaction.Reference).IsUnique();
        builder.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(transaction => transaction.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasMany(transaction => transaction.Entries)
            .WithOne()
            .HasForeignKey(entry => entry.LedgerTransactionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(transaction => transaction.Entries)
            .HasField("_entries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
