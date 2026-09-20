namespace FinCore.Application.Features.Auth.Login;
public sealed record LoginResult(string AccessToken, int ExpiresIn);
