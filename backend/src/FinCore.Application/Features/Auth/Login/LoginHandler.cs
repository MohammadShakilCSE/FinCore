using FinCore.Application.Abstractions.Authentication;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Domain.Entities;
namespace FinCore.Application.Features.Auth.Login;
public sealed class LoginHandler(IUserRepository users, IPasswordService passwords, ITokenService tokens)
{
    // A dummy hash keeps unknown-account requests on the password verification path too.
    private static readonly User Dummy = User.Create("Unknown", "unknown@example.invalid");
    private readonly string dummyHash = passwords.HashPassword(Dummy, Guid.NewGuid().ToString());
    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Password is null || command.Password.Length > 128 || command.Email is null || command.Email.Length > 256)
            throw new InvalidCredentialsException();
        var user = await users.GetByEmailAsync(command.Email, cancellationToken);
        var check = passwords.VerifyPassword(user ?? Dummy, user?.PasswordHash ?? dummyHash, command.Password);
        if (user is null || !user.IsActive || check == PasswordCheck.Failed) throw new InvalidCredentialsException();
        if (check == PasswordCheck.RehashNeeded)
        {
            user.SetPasswordHash(passwords.HashPassword(user, command.Password));
            await users.SaveChangesAsync(cancellationToken);
        }
        var token = tokens.Generate(user);
        return new LoginResult(token.Token, token.ExpiresIn);
    }
}
