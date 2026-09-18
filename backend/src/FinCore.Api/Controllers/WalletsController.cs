using FinCore.Api.Contracts.Requests;
using FinCore.Application.Features.Wallets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class WalletsController : ControllerBase
    {
        private readonly CreateWalletHandler _handler;

        public WalletsController(CreateWalletHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            CreateWalletRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateWalletCommand(
                request.OwnerId,
                request.Currency);

            var result = await _handler.HandleAsync(
                command,
                cancellationToken);

            return Ok(result);
        }
    }
}
