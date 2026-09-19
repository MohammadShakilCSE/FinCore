namespace FinCore.Application.Abstractions.Identity;

// Must be supplied by verified authentication, never by request-body owner IDs.
public interface ICurrentOwner
{
    Guid GetRequiredOwnerId();
}
