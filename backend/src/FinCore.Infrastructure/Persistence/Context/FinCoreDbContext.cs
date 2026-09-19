using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Infrastructure.Persistence.Context;

public sealed class FinCoreDbContext(DbContextOptions<FinCoreDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinCoreDbContext).Assembly);
}
