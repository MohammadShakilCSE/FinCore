using FinCore.Application.Idempotency;
using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords", table => table.HasCheckConstraint("CK_IdempotencyRecords_Lifecycle",
            "(\"Status\" = 'Processing' AND \"TransactionId\" IS NULL AND \"ResponsePayload\" IS NULL AND \"CompletedAt\" IS NULL) OR " +
            "(\"Status\" = 'Completed' AND \"TransactionId\" IS NOT NULL AND \"ResponsePayload\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)"));
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.OwnerId).IsRequired();
        builder.Property(record => record.Operation).HasMaxLength(64).UseCollation("C").IsRequired();
        builder.Property(record => record.IdempotencyKey).HasMaxLength(128).UseCollation("C").IsRequired();
        builder.Property(record => record.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(record => record.Status).HasMaxLength(16).IsRequired();
        builder.Property(record => record.ResponsePayload).HasMaxLength(4096);
        builder.Property(record => record.CreatedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(record => record.CompletedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(record => new { record.OwnerId, record.Operation, record.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_IdempotencyRecords_Owner_Operation_Key");
        builder.HasOne<LedgerTransaction>().WithMany().HasForeignKey(record => record.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
