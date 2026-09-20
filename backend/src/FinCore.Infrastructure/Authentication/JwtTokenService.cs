using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinCore.Application.Abstractions.Authentication;
using FinCore.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace FinCore.Infrastructure.Authentication;
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public AccessToken Generate(User user)
    {
        var settings = options.Value;
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim("sub", user.Id.ToString()), new Claim("email", user.Email),
             new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
             new Claim("jti", Guid.NewGuid().ToString())], now, now.AddMinutes(settings.LifetimeMinutes),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), settings.LifetimeMinutes * 60);
    }
}
