using FinCore.Api.Contracts.Requests;
using FinCore.Application.Features.Wallets;
using FinCore.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers;

[Route("api/wallets")]
[ApiController]
public sealed class WalletsController(CreateWalletHandler createHandler, GetWalletHandler getHandler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateWalletRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await createHandler.HandleAsync(
                new CreateWalletCommand(request.OwnerId, request.Currency), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.WalletId }, result);
        }
        catch (DomainException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(new GetWalletQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
