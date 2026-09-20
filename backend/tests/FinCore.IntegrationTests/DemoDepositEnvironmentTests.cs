using FinCore.Api.Contracts.Requests;
using FinCore.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
namespace FinCore.IntegrationTests;
public sealed class DemoDepositEnvironmentTests
{
    [Fact]
    public async Task Public_demo_deposit_is_disabled_without_accessing_persistence()
    {
        var controller = new WalletsController(null!, null!);
        var response = await controller.DemoDeposit(Guid.NewGuid(), new DemoDepositRequest(500, "BDT"), default);
        Assert.IsType<NotFoundResult>(response);
    }
}
