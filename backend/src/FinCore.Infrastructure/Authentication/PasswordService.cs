using FinCore.Application.Abstractions.Authentication;
using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
namespace FinCore.Infrastructure.Authentication;
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> hasher = new();
    public string HashPassword(User user, string password) => hasher.HashPassword(user, password);
    public PasswordCheck VerifyPassword(User user, string passwordHash, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(user, passwordHash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.RehashNeeded,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException) { return PasswordCheck.Failed; }
    }
}
