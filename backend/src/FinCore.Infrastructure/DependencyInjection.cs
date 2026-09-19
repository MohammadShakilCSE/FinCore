using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Abstractions.Identity;
using FinCore.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FinCore.Infrastructure.Persistence.Context;
using FinCore.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FinCoreDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ILedgerTransactionRepository, LedgerTransactionRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.TryAddScoped<ICurrentOwner, UnauthenticatedOwner>();
        return services;
    }
}
