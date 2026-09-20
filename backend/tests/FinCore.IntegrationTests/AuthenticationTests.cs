using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinCore.Application.Exceptions;
using FinCore.Application.Features.Auth.Register;
using FinCore.Application.Features.Auth.Login;
using FinCore.Application.Features.Wallets;
using FinCore.Application.Abstractions.Authentication;
using FinCore.Domain.Entities;
using FinCore.Domain.ValueObjects;
using FinCore.Infrastructure.Authentication;
using FinCore.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FinCore.Infrastructure.Persistence.Context;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;

namespace FinCore.IntegrationTests;

public sealed class AuthenticationTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private const string Password = "ExamplePassword123!";
    private static JwtOptions Settings() => new() { Issuer = "test", Audience = "customers", SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };

    [Fact]
    public async Task Api_rejects_missing_signing_key_at_startup()
    {
        var settings = Settings(); settings.SigningKey = "";
        await using var factory = new ApiFactory("Host=localhost;Database=unused", settings);
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public void Password_hash_is_salted_and_verifies_only_correct_password()
    {
        var user = User.Create("Shakil", " SHAKIL@example.com ");
        var service = new PasswordService();
        var hash = service.HashPassword(user, Password);
        Assert.NotEqual(Password, hash);
        Assert.NotEqual(hash, service.HashPassword(user, Password));
        Assert.Equal(PasswordCheck.Success, service.VerifyPassword(user, hash, Password));
        Assert.Equal(PasswordCheck.Failed, service.VerifyPassword(user, hash, "incorrect"));
        Assert.Equal("shakil@example.com", user.Email);
        Assert.True(user.IsActive);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
    }

    [Fact]
    public void Jwt_validates_signature_issuer_audience_lifetime_and_subject()
    {
        var settings = Settings();
        var user = User.Create("Shakil", "shakil@example.com");
        var token = new JwtTokenService(Options.Create(settings)).Generate(user);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token.Token, settings.ValidationParameters(), out _);
        Assert.Equal(user.Id.ToString(), principal.FindFirst("sub")?.Value);
        Assert.NotNull(principal.FindFirst("iat"));
        Assert.Equal(1800, token.ExpiresIn);
        var wrongKey = Settings();
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token.Token, wrongKey.ValidationParameters(), out _));
        var wrongIssuer = settings.ValidationParameters(); wrongIssuer.ValidIssuer = "other";
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => handler.ValidateToken(token.Token, wrongIssuer, out _));
        var wrongAudience = settings.ValidationParameters(); wrongAudience.ValidAudience = "other";
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(token.Token, wrongAudience, out _));
    }

    [Fact]
    public async Task Login_rate_limiter_returns_429_after_ten_attempts()
    {
        await using var factory = new ApiFactory("Host=localhost;Database=unused", Settings());
        using var client = factory.CreateClient();
        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "unknown@example.com", password = new string('x', 129) });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var limited = await client.PostAsJsonAsync("/api/auth/login", new { email = "unknown@example.com", password = new string('x', 129) });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public void Current_user_requires_authenticated_valid_subject()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var current = new FinCore.Api.Authentication.CurrentUser(new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context });
        Assert.False(current.IsAuthenticated);
        Assert.Throws<AuthenticationRequiredException>(() => current.UserId);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]));
        Assert.Throws<AuthenticationRequiredException>(() => current.UserId);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "invalid")], "Bearer"));
        Assert.Throws<AuthenticationRequiredException>(() => current.UserId);
        var id = Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())], "Bearer"));
        Assert.Equal(id, current.UserId);
        Assert.Equal(id, current.GetRequiredOwnerId());
    }

    [PostgresFact]
    public async Task Registration_persists_hash_and_database_rejects_duplicate_email_races()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var service = new PasswordService();
        async Task<bool> Register(FinCore.Infrastructure.Persistence.Context.FinCoreDbContext context)
        {
            try { await new RegisterHandler(new UserRepository(context), service).HandleAsync(new("Shakil", email.ToUpperInvariant(), Password)); return true; }
            catch (DuplicateEmailException) { return false; }
        }
        var results = await Task.WhenAll(Register(first), Register(second));
        Assert.Single(results, x => x);
        await using var check = database.CreateContext();
        var user = await check.Users.SingleAsync(u => u.Email == email);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(PasswordCheck.Success, service.VerifyPassword(user, user.PasswordHash, Password));
        // Bypass the application pre-check to verify the repository's database-race translation.
        var duplicate = User.Create("Karim", email);
        duplicate.SetPasswordHash(service.HashPassword(duplicate, Password));
        await Assert.ThrowsAsync<DuplicateEmailException>(() => new UserRepository(check).AddAsync(duplicate));
    }

    [PostgresFact]
    public async Task Login_rejects_unknown_wrong_password_and_inactive_and_upgrades_old_hash()
    {
        await using var context = database.CreateContext();
        var user = User.Create("Shakil", $"{Guid.NewGuid():N}@example.com");
        var legacy = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2 }));
        var oldHash = legacy.HashPassword(user, Password);
        user.SetPasswordHash(oldHash);
        var repository = new UserRepository(context);
        await repository.AddAsync(user);
        var handler = new LoginHandler(repository, new PasswordService(), new JwtTokenService(Options.Create(Settings())));
        var missing = await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(new("missing@example.com", Password)));
        var wrong = await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(new(user.Email, "incorrect")));
        var result = await handler.HandleAsync(new(user.Email.ToUpperInvariant(), Password));
        Assert.NotEmpty(result.AccessToken);
        await using var check = database.CreateContext();
        Assert.NotEqual(oldHash, (await check.Users.SingleAsync(u => u.Id == user.Id)).PasswordHash);
        user.Deactivate(); await repository.SaveChangesAsync();
        var inactive = await Assert.ThrowsAsync<InvalidCredentialsException>(() => handler.HandleAsync(new(user.Email, Password)));
        Assert.Equal(missing.Message, wrong.Message); Assert.Equal(wrong.Message, inactive.Message);
    }

    [PostgresFact]
    public async Task Http_authentication_wallet_ownership_and_idempotency_are_enforced()
    {
        var settings = Settings();
        await using var factory = new ApiFactory(database.ConnectionString, settings);
        // Real loopback HTTP, including the production middleware and controllers.
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(new Npgsql.NpgsqlConnectionStringBuilder(database.ConnectionString).Database,
                scope.ServiceProvider.GetRequiredService<FinCoreDbContext>().Database.GetDbConnection().Database);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/wallets", new { currency = "BDT" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var expired = new JwtSecurityToken(settings.Issuer, settings.Audience, [new Claim("sub", Guid.NewGuid().ToString())],
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(expired));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var invalid = await client.PostAsJsonAsync("/api/auth/register", new { name = "", email = "bad", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        async Task<(Guid Id, string Token)> Register(string name)
        {
            var email = $"{Guid.NewGuid():N}@example.com";
            var response = await client.PostAsJsonAsync("/api/auth/register", new { name, email, password = Password });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
            var registration = (await response.Content.ReadFromJsonAsync<RegisterResult>())!;
            var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { name, email = email.ToUpperInvariant(), password = Password });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
            login.EnsureSuccessStatusCode();
            return (registration.UserId, (await login.Content.ReadFromJsonAsync<LoginResult>())!.AccessToken);
        }
        var shakil = await Register("Shakil"); var karim = await Register("Karim");
        client.DefaultRequestHeaders.Authorization = new("Bearer", shakil.Token);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(shakil.Id, me.GetProperty("id").GetGuid());
        Assert.False(me.TryGetProperty("passwordHash", out _));
        async Task<CreateWalletResult> Create(Guid forgedOwner)
        {
            var response = await client.PostAsJsonAsync("/api/wallets", new { currency = "BDT", ownerId = forgedOwner });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CreateWalletResult>())!;
        }
        var sender = await Create(karim.Id);
        Assert.Equal(shakil.Id, sender.OwnerId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/wallets/{sender.WalletId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/wallets/{sender.WalletId}/demo-deposits", new { amount = 500, currency = "BDT" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", karim.Token);
        var receiver = await Create(shakil.Id);
        Assert.Equal(karim.Id, receiver.OwnerId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/wallets/{sender.WalletId}")).StatusCode);
        await using (var context = database.CreateContext())
        {
            var wallet = await context.Wallets.SingleAsync(w => w.Id == sender.WalletId);
            wallet.Credit(new Money(1000, "BDT")); await context.SaveChangesAsync();
        }
        var key = Guid.NewGuid().ToString("N");
        var command = new { senderWalletId = sender.WalletId, receiverWalletId = receiver.WalletId, amount = 100, currency = "BDT", idempotencyKey = key };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/transfers", command)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", shakil.Token);
        var transfer = await client.PostAsJsonAsync("/api/transfers", command); transfer.EnsureSuccessStatusCode();
        var replay = await client.PostAsJsonAsync("/api/transfers", command); replay.EnsureSuccessStatusCode();
        Assert.Equal(await transfer.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new("Bearer", karim.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/transfers", command)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/transfers/attempts/{key}")).StatusCode);
        await using var final = database.CreateContext();
        Assert.Equal(900, (await final.Wallets.SingleAsync(w => w.Id == sender.WalletId)).Balance.Amount);
        Assert.Equal(100, (await final.Wallets.SingleAsync(w => w.Id == receiver.WalletId)).Balance.Amount);
        var records = await final.IdempotencyRecords.Where(i => i.OwnerId == shakil.Id).ToListAsync();
        Assert.Single(records);
    }

    private sealed class ApiFactory(string connection, JwtOptions settings) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Program captures its connection before WebApplicationFactory's configuration callback.
                // Replace the context explicitly so HTTP tests can never write to the development database.
                services.RemoveAll<FinCoreDbContext>();
                services.AddScoped(_ => new FinCoreDbContext(new DbContextOptionsBuilder<FinCoreDbContext>().UseNpgsql(connection).Options));
            });
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FinCore"] = connection,
                ["Jwt:Issuer"] = settings.Issuer, ["Jwt:Audience"] = settings.Audience,
                ["Jwt:SigningKey"] = settings.SigningKey,
                ["DemoDeposits:Enabled"] = "true"
            }));
        }
    }
}
