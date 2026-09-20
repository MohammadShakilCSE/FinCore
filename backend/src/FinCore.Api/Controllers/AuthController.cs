using FinCore.Application.Abstractions.Authentication;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Features.Auth.Register;
using FinCore.Application.Features.Auth.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace FinCore.Api.Controllers;
[ApiController]
[Route("api/auth")]
public sealed class AuthController(RegisterHandler register, LoginHandler login, ICurrentUser currentUser, IUserRepository users) : ControllerBase
{
    [AllowAnonymous, HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken cancellationToken)
        => StatusCode(201, await register.HandleAsync(command, cancellationToken));
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
        => Ok(await login.HandleAsync(command, cancellationToken));
    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(currentUser.UserId, cancellationToken);
        return user is null || !user.IsActive ? Unauthorized() : Ok(new { user.Id, user.Name, user.Email });
    }
}
