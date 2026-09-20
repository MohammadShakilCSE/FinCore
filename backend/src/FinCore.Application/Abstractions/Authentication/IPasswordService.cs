using FinCore.Domain.Entities;
namespace FinCore.Application.Abstractions.Authentication;
public enum PasswordCheck { Failed, Success, RehashNeeded }
public interface IPasswordService
{
    string HashPassword(User user, string password);
    PasswordCheck VerifyPassword(User user, string passwordHash, string password);
}
