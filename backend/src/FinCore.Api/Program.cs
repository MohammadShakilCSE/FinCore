using FinCore.Application.Features.Wallets;
using FinCore.Application.Features.Wallets.DemoDeposit;
using FinCore.Infrastructure;
using FinCore.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("FinCore")
    ?? throw new InvalidOperationException("ConnectionStrings:FinCore is required."));
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<GetWalletHandler>();
builder.Services.AddScoped<DemoDepositHandler>();

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
