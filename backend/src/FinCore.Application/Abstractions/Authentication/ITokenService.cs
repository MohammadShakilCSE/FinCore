using FinCore.Domain.Entities;
namespace FinCore.Application.Abstractions.Authentication;
public sealed record AccessToken(string Token, int ExpiresIn);
public interface ITokenService { AccessToken Generate(User user); }
