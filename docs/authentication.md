# Authentication

Start from `backend` in PowerShell. Supply a cryptographically random signing key through an environment variable before starting the API. Nothing secret belongs in the checked-in JWT settings:

```powershell
$env:Jwt__SigningKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet run --project src/FinCore.Api
```

This temporary key is for the current process/session. Keep a stable secret in your deployment's secret store; changing the key invalidates existing tokens. `Jwt:Issuer`, `Jwt:Audience`, and `Jwt:LifetimeMinutes` are configured in appsettings; the default lifetime is 30 minutes (allowed range 1?60). Startup rejects missing/invalid JWT settings. There are no refresh tokens.

Use `backend/src/FinCore.Api/FinCore.Api.http` for registration, login, `/me`, wallets, and transfers.

Registration trims the name, normalizes email to trimmed lowercase, validates fields and a 12?128 character password, and stores only a salted framework password hash. ASP.NET Core Identity's PasswordHasher uses PBKDF2; we do not implement cryptography. PostgreSQL enforces the normalized email format and a unique email index. Simultaneous registrations result in one success and one safe 409 response. Invalid data returns 400. The response contains only UserId.

Login verifies the framework hash and active account status, upgrades legacy hashes using repository SaveChanges, and returns an access token and seconds until expiry. Unknown email, wrong password, and inactive user all return the same 401 error. Login and registration share a per-IP limit of 10 requests per minute, then return 429. The limit is process-local; deployments behind proxies must configure trusted forwarding and distributed protection separately.

JWTs contain subject (User ID), email, issued-at time, unique token ID, issuer, audience, and expiration. Bearer middleware validates the HMAC-SHA256 signature, issuer, audience, and expiry with no expiry grace period. ICurrentUser reads the subject only from the authenticated HttpContext principal. Application never depends on HttpContext. The API also supplies this identity through the existing ICurrentOwner interface to preserve transfer/idempotency workflows.

Shakil's wallet is created with Shakil's authenticated ID. Any OwnerId supplied in JSON is ignored and no longer part of the request contract. Karim gets 404 when looking up Shakil's wallet. If Karim sends Shakil's sender-wallet ID in Postman with Karim's token, the transfer returns 403 before claiming an idempotency record or moving money. Missing/invalid/expired tokens return 401.

Idempotency remains scoped by authenticated owner, operation, and key. Shakil's exact retry returns the saved result without another debit. Karim cannot spend from Shakil's wallet or retrieve his result using a copied key. Existing ledger, transaction, and optimistic concurrency behavior is preserved.

Public demo deposits now return 404 even in Development with the old flag enabled. Tests seed simulated balances directly in disposable databases. Existing wallets retain their owner IDs; authentication does not automatically reassign legacy wallets to new users.

Deactivating a user blocks subsequent logins and `/me`. Already issued access tokens remain valid for other protected actions until expiry; token revocation is outside this simple JWT implementation.

The AddUserAuthentication migration creates only Users, its constraints, and its unique index. It does not modify existing financial tables.

```powershell
dotnet build
# Set FinCore_TestConnectionString to a local PostgreSQL connection with CREATEDB permission.
dotnet test
dotnet ef migrations add AddUserAuthentication --project src/FinCore.Infrastructure --startup-project src/FinCore.Api --output-dir Persistence/Migrations
# Migration already exists: do not generate it a second time.
dotnet ef database update --project src/FinCore.Infrastructure --startup-project src/FinCore.Api
```

The design-time factory currently reads the local connection in API appsettings. Inspect the target before applying migrations. PostgreSQL tests create and remove randomly named `fincore_test_*` databases. The authentication HTTP integration test uses real Kestrel loopback HTTP, real PostgreSQL, the actual controllers, and production authentication middleware.

## Changed files

- Domain: added `Entities/User.cs`.
- Application: added `Abstractions/Authentication/{ICurrentUser,IPasswordService,ITokenService}.cs`, `Abstractions/Persistence/IUserRepository.cs`, `Exceptions/{DuplicateEmailException,InvalidCredentialsException}.cs`, and the command/result/handler files under `Features/Auth/{Register,Login}`. Updated `Features/Wallets/{CreateWalletCommand,CreateWalletHandler,GetWalletHandler}.cs`.
- Infrastructure: added `Authentication/{PasswordService,JwtOptions,JwtTokenService}.cs`, `Persistence/Configurations/UserConfiguration.cs`, `Persistence/Repositories/UserRepository.cs`, and the `AddUserAuthentication` migration/designer. Updated `DependencyInjection.cs`, `FinCore.Infrastructure.csproj`, `Persistence/Context/FinCoreDbContext.cs`, and the model snapshot.
- API: added `Authentication/CurrentUser.cs` and `Controllers/{AuthController,TransfersController}.cs`. Updated `Controllers/WalletsController.cs`, `Contracts/Requests/CreateWalletRequest.cs`, `Middleware/ExceptionHandlingMiddleware.cs.cs`, `Program.cs`, `FinCore.Api.csproj`, both appsettings files, and `FinCore.Api.http`.
- Tests: added `AuthenticationTests.cs`; updated `DemoDepositEnvironmentTests.cs`, `PostgresFixture.cs`, and `FinCore.IntegrationTests.csproj`.
- Documentation: added this guide and updated `README.md`.

Framework reference: [Microsoft JWT bearer authentication documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).

## Verification performed

- `dotnet build`: succeeded, zero warnings and zero errors.
- `dotnet test` completed successfully after the HTTP fixture correction; final `dotnet test --no-build --nologo` against the final build: 136 passed, 0 failed, 0 skipped (31 Domain, 38 Application, 67 Integration).
- Real PostgreSQL covered registration uniqueness/races, persisted password hashes, hash upgrades, inactive accounts, ledger/concurrency/idempotency behavior, and the actual HTTP flow. Kestrel HTTP covered registration, login, `/me`, missing/invalid/expired tokens, wallet ownership, forbidden transfers, authorized replay, copied-key isolation, and disabled demo deposits. Separate tests covered rate limiting and missing signing-key startup failure.
- Generated and reviewed `20260920190349_AddUserAuthentication`; applied it to `localhost:5432/fincore`. EF also applied the previously pending `20260919192314_AddTransferIdempotency`. Confirmed both in migration history. No existing financial tables were dropped.
- An initial HTTP test configuration error used the development connection. Its two synthetic users and two empty wallets were removed by exact ID. The corrected fixture explicitly supplies its disposable context and asserts the target database before HTTP requests. Final development Users count was zero.
