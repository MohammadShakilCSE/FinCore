using FinCore.Application.Features.Wallets;
using FinCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("FinCore")
    ?? throw new InvalidOperationException("ConnectionStrings:FinCore is required."));
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<GetWalletHandler>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
