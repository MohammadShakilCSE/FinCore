using FinCore.Application.Abstractions.Authentication;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Exceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Exceptions;
namespace FinCore.Application.Features.Auth.Register;
public sealed class RegisterHandler(IUserRepository users, IPasswordService passwords)
{
    public async Task<RegisterResult> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        var user = User.Create(command.Name, command.Email);
        if (command.Password is null || command.Password.Length is < 12 or > 128)
            throw new DomainException("Password must contain 12 to 128 characters.");
        if (await users.GetByEmailAsync(user.Email, cancellationToken) is not null) throw new DuplicateEmailException();
        user.SetPasswordHash(passwords.HashPassword(user, command.Password));
        await users.AddAsync(user, cancellationToken);
        return new RegisterResult(user.Id);
    }
}
