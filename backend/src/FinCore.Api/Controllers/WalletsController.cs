using FinCore.Api.Contracts.Requests;
using FinCore.Application.Features.Wallets;
using FinCore.Application.Features.Wallets.DemoDeposit;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

[Route("api/wallets")]
[ApiController]
public sealed class WalletsController(
    CreateWalletHandler createHandler,
    GetWalletHandler getHandler,
    DemoDepositHandler demoDepositHandler,
    IHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateWalletRequest request, CancellationToken cancellationToken)
    {
        var result = await createHandler.HandleAsync(
            new CreateWalletCommand(request.OwnerId, request.Currency), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.WalletId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(new GetWalletQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{walletId:guid}/demo-deposits")]
    public async Task<IActionResult> DemoDeposit(
        Guid walletId, DemoDepositRequest request, CancellationToken cancellationToken)
    {
        // Simulated funds only: a flag alone must never enable this in production.
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing")) ||
            !configuration.GetValue<bool>("DemoDeposits:Enabled"))
            return NotFound();

        var result = await demoDepositHandler.HandleAsync(
            new DemoDepositCommand(walletId, request.Amount, request.Currency), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
