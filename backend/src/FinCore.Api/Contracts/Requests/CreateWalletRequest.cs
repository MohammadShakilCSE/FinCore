namespace FinCore.Api.Contracts.Requests
{
    public sealed record CreateWalletRequest(
     Guid OwnerId,
     string Currency);
}
