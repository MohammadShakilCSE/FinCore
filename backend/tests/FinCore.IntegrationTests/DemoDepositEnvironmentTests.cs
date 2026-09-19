using FinCore.Api.Contracts.Requests;
using FinCore.Api.Controllers;
using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Features.Wallets;
using FinCore.Application.Features.Wallets.DemoDeposit;
using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FinCore.IntegrationTests;

// These boundary tests need no PostgreSQL: forbidden calls must never reach the repository.
public sealed class DemoDepositEnvironmentTests
{
    [Theory]
    [InlineData("Production", "true", false)]
    [InlineData("Staging", "true", false)]
    [InlineData("Development", "false", false)]
    [InlineData("Testing", "false", false)]
    [InlineData("Development", null, false)]
    [InlineData("Testing", null, false)]
    [InlineData("Development", "true", true)]
    [InlineData("Testing", "true", true)]
    public async Task Requires_allowed_environment_and_explicit_flag(string environment, string? enabled, bool allowed)
    {
        var repository = new GuardRepository();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DemoDeposits:Enabled"] = enabled }).Build();
        var controller = new WalletsController(new CreateWalletHandler(repository),
            new GetWalletHandler(repository), new DemoDepositHandler(repository),
            new TestEnvironment { EnvironmentName = environment }, configuration);

        var response = await controller.DemoDeposit(repository.Wallet.Id, new DemoDepositRequest(500, "BDT"), default);

        Assert.Equal(allowed, repository.Loaded);
        Assert.Equal(allowed, repository.Saved);
        if (allowed) Assert.IsType<OkObjectResult>(response);
        else Assert.IsType<NotFoundResult>(response);
    }

    private sealed class GuardRepository : IWalletRepository
    {
        public Wallet Wallet { get; } = Wallet.Create(Guid.NewGuid(), "BDT");
        public bool Loaded { get; private set; }
        public bool Saved { get; private set; }
        public Task<Wallet?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Loaded = true;
            return Task.FromResult<Wallet?>(Wallet);
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "FinCore.Api";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
