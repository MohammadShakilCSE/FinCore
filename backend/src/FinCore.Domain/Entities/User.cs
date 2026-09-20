using FinCore.Domain.Common;
using FinCore.Domain.Exceptions;
using System.Net.Mail;
namespace FinCore.Domain.Entities;
public sealed class User : Entity
{
    private User() { }
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public bool IsActive { get; private set; } = true;
    public static string NormalizeEmail(string email) => (email ?? "").Trim().ToLowerInvariant();
    public static User Create(string name, string email)
    {
        name = (name ?? "").Trim();
        email = NormalizeEmail(email);
        if (name.Length is < 1 or > 150) throw new DomainException("Name must contain 1 to 150 characters.");
        if (email.Length > 256 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new DomainException("A valid email is required.");
        return new User { Name = name, Email = email };
    }
    public void SetPasswordHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) throw new DomainException("Password hash is required.");
        PasswordHash = hash;
    }
    public void Deactivate() => IsActive = false;
}
