using FinCore.Application.Abstractions.Identity;

namespace FinCore.IntegrationTests;

internal sealed class TestOwner(Guid ownerId) : ICurrentOwner
{
    public Guid GetRequiredOwnerId() => ownerId;
}
