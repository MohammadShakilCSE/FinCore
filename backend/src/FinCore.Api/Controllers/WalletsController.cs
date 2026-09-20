using FinCore.Api.Contracts.Requests;
using FinCore.Application.Features.Wallets;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

[Route("api/wallets")]
[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class WalletsController(
    CreateWalletHandler createHandler,
    GetWalletHandler getHandler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateWalletRequest request, CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(
            new CreateWalletCommand(request.Currency), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.WalletId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(new GetWalletQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{walletId:guid}/demo-deposits")]
    public Task<IActionResult> DemoDeposit(
        Guid walletId, DemoDepositRequest request, CancellationToken cancellationToken)
    {
        // Public funding is disabled until an appropriate operator authorization mechanism exists.
        return Task.FromResult<IActionResult>(NotFound());
    }
}
