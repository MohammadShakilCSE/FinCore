using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Domain.Entities;
using FinCore.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace FinCore.Infrastructure.Persistence.Repositories;
public sealed class UserRepository(FinCoreDbContext context) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => context.Users.SingleOrDefaultAsync(u => u.Email == User.NormalizeEmail(email), cancellationToken);
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Users_Email" })
        {
            context.Entry(user).State = EntityState.Detached;
            throw new DuplicateEmailException();
        }
    }
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) => await context.SaveChangesAsync(cancellationToken);
}
