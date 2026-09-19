using FinCore.Application.Abstractions.Identity;
using FinCore.Application.Exceptions;

namespace FinCore.Infrastructure.Identity;

// No authentication exists yet. Fail closed until a verified identity adapter replaces this.
public sealed class UnauthenticatedOwner : ICurrentOwner
{
    public Guid GetRequiredOwnerId() => throw new AuthenticationRequiredException();
}
