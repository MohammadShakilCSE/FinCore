using FinCore.Application.Abstractions.Authentication;
using FinCore.Application.Abstractions.Identity;
using FinCore.Application.Exceptions;
namespace FinCore.Api.Authentication;
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser, ICurrentOwner
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    public Guid UserId => IsAuthenticated && Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) && id != Guid.Empty
        ? id : throw new AuthenticationRequiredException();
    public Guid GetRequiredOwnerId() => UserId;
}
