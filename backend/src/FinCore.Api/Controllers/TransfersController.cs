using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Application.Features.Transfers.GetTransferAttempt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FinCore.Api.Controllers;
[ApiController, Authorize, Route("api/transfers")]
public sealed class TransfersController(TransferMoneyHandler transfer, GetTransferAttemptHandler attempts) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Transfer(TransferMoneyCommand command, CancellationToken cancellationToken)
        => Ok(await transfer.HandleAsync(command, cancellationToken));
    [HttpGet("attempts/{key}")]
    public async Task<IActionResult> Attempt(string key, CancellationToken cancellationToken)
    {
        var result = await attempts.HandleAsync(new(key), cancellationToken);
        return result.Status == TransferAttemptStatus.Unavailable ? NotFound() : Ok(result);
    }
}
