# Demo Deposit

Current progress: [Wallet optimistic concurrency](wallet-concurrency.md) now protects
stale writes from deposits and transfers. The concurrency limitations below describe
the earlier demo-deposit lesson; idempotency and reconciliation are still pending.

Demo Deposit adds simulated money to an existing wallet. It is a learning operation,
not a payment, bank deposit, or ledger transaction. No additional infrastructure or
database migration is needed: the existing balance column stores the result.

## Try it

The endpoint is available only when BOTH conditions hold:

- The host environment is `Development` or `Testing`.
- `DemoDeposits:Enabled` is explicitly `true`.

`appsettings.Development.json` enables it for local development. Missing or false
configuration disables it. Production, Staging, and other environments always receive
404, even if someone sets the flag to true. Testing must explicitly supply the flag.
This is an environment restriction, not user authentication or permission to expose
simulated funds publicly.

From the repository root, with PostgreSQL configured in API's appsettings:

```powershell
dotnet run --project backend/src/FinCore.Api --launch-profile http
```

Create a wallet using the existing POST endpoint, then use its returned walletId:

```http
POST /api/wallets/{walletId}/demo-deposits
Content-Type: application/json

{
  "amount": 500,
  "currency": "BDT"
}
```

For a wallet whose balance was 1000, a successful response is HTTP 200:

```json
{
  "walletId": "0f3245ab-f3eb-49cc-be8a-0d6ff83060bf",
  "depositedAmount": 500,
  "currentBalance": 1500,
  "currency": "BDT"
}
```

A missing wallet returns 404. Zero/negative amounts, mismatched currency, and inactive
wallet operations produce DomainException and the existing global middleware returns
400 with `application/problem+json`. Unexpected errors use its logged 500 response.
The controller has no try/catch. GET still returns a read-only snapshot of the wallet.

## Why the code lives in these layers

**Application coordinates a use case.** DemoDepositHandler answers “what steps should
happen when someone requests a demo deposit?” It loads the wallet, constructs Money,
asks the wallet to credit it, saves, and returns the result. It depends on
IWalletRepository, not SQL or DbContext. A missing wallet returns null, matching the
existing GetWalletHandler convention; the controller translates that to HTTP 404.

**Domain decides what is allowed.** Wallet.Credit already requires an active wallet,
matching currency, and a positive amount. Money rejects negative amounts, invalid
currency codes, and unsupported precision/range. Reusing these methods protects the
rules in every caller; duplicating them in a handler would let the rules drift apart.
Balance is changed only by calling `wallet.Credit(money)`.

**Tracking means EF remembers an object it loaded.** GetTrackedByIdAsync explicitly
uses AsTracking so the context remembers the wallet and its original values. Calling
Credit replaces the immutable Money value. At save time, EF detects the mapped balance
change and issues the corresponding database update. The owned Money mapping stays
unchanged. GetByIdAsync continues using AsNoTracking for queries; changing an object
returned by that read method would not automatically be saved.

**SaveChangesAsync commits the pending changes.** The new repository method delegates
to the same scoped context that loaded the wallet. The handler awaits it before
returning success. If saving fails, no successful deposit result is returned. Saving
flushes all pending changes on that context, not only one property. AddAsync still
saves immediately, so CreateWallet behavior is preserved. A future multi-step use case
must deliberately review this save boundary.

**The controller translates HTTP.** It checks whether this simulation endpoint is
enabled, builds a command from route/body input, calls the handler, and returns 200 or
404. Business rules and persistence stay in their respective layers. Domain exceptions
travel to the existing global middleware rather than being caught in every action.

## Patterns reused and their costs

| Pattern | Problem it solves | How this feature uses it | Cost / when not to add more |
| --- | --- | --- | --- |
| Repository | Application should not depend on EF or SQL | IWalletRepository adds tracked lookup and save; WalletRepository implements both | Adds a layer over EF. No generic repository or IQueryable exposure is needed. |
| Dependency Injection | Handlers should not construct database dependencies | Program registers DemoDepositHandler as scoped; existing repository/context registrations supply the same scoped session | Registration and lifetime choices must be correct. No new container or duplicate registration is needed. |
| Application handler / command | HTTP handling should not own the business workflow | DemoDepositCommand describes intent; the handler coordinates it and returns DemoDepositResult | Extra types, but matches existing use cases. No MediatR or command bus is needed. |
| Aggregate and value object | Balance rules must be enforced consistently | Existing Wallet controls mutation; existing Money keeps amount and currency together | Mapping immutable values needs care. Reuse these types rather than creating deposit-specific business rules. |

DbContext already tracks work and saves it. An additional Unit of Work abstraction is
not needed for this single-wallet operation; adding one now would duplicate machinery
without a new coordination requirement.

The existing lifecycle has a limitation: Close() sets Suspended, and Activate() rejects
that state. There is no separate Closed enum member. Tests call both Close() and
Suspend() and verify deposits are rejected; this feature does not redefine lifecycle
semantics or change existing business rules.

## Concurrent deposits are not safe financial processing

Two requests can both read 1000. One adds 500 and saves 1500; the other adds 200 and
saves 1200. The last write can overwrite the first, leaving 1200 instead of 1700.
Tracking and SaveChanges do not prevent this lost update. This feature introduces no
concurrency token, locking strategy, or atomic increment, and makes no concurrency-safety
claim.

Retrying the same successful request can also credit twice: there is no idempotency
key or ledger entry. Idempotency means repeated delivery of one operation has the same
effect as processing it once. Upcoming transfer/ledger lessons must address both
concurrency and idempotency properly before real financial use. No distributed locks,
messaging, or other unrelated technologies are introduced here.

## Verification

The full solution built with 0 warnings and 0 errors. All 59 tests passed with no skips:

- 18 existing Domain tests.
- 9 new Application tests: success, zero, negative, currency mismatch, blank currency,
  suspended wallet, closed wallet, missing wallet, and propagation of a save failure.
- 24 actual PostgreSQL tests: 17 existing plus 7 new deposit cases. New tests create the
  wallet in one context, deposit in another, and verify the stored balance in a fresh
  context. Invalid deposits are checked for unchanged stored balances.
- 8 environment/flag boundary tests in the IntegrationTests project, which intentionally
  do not use a database. They prove forbidden requests never call the repository.

Commands used:

```powershell
dotnet build backend/FinCore.sln --no-restore
# Only the existing test fixture uses this variable; API/migrations keep their JSON configuration.
$settings = Get-Content backend/src/FinCore.Api/appsettings.json -Raw | ConvertFrom-Json
$env:FinCore_TestConnectionString = $settings.ConnectionStrings.FinCore
dotnet test backend/FinCore.sln --no-build --no-restore
```

The PostgreSQL fixture creates and removes only uniquely named test databases. Its
configured local role needs CREATEDB. This uses the existing test infrastructure,
without mocks of DbContext, Docker, or Testcontainers.

Live verification against the configured localhost:5432/fincore database also passed:

1. Started the API in Development on port 5297.
2. Created wallet `0f3245ab-f3eb-49cc-be8a-0d6ff83060bf` (HTTP 201).
3. Deposited 1000, then 500 BDT (HTTP 200 for both).
4. GET returned 1500 BDT.
5. Direct psql SELECT confirmed Balance=1500.0000 and Currency=BDT.
6. Zero, negative, and wrong-currency HTTP requests returned 400; a missing wallet returned 404.
7. Started another local API in Production on port 5298 with the enable flag true.
   A deposit returned 404 and the wallet still held 1500.

Both verification API processes are stopped afterward. The demo wallet remains in the
local database for inspection. No verification steps are incomplete.

## Changed files

Created:

- `backend/src/FinCore.Application/Features/Wallets/DemoDeposit/DemoDepositCommand.cs`
- `backend/src/FinCore.Application/Features/Wallets/DemoDeposit/DemoDepositResult.cs`
- `backend/src/FinCore.Application/Features/Wallets/DemoDeposit/DemoDepositHandler.cs`
- `backend/src/FinCore.Api/Contracts/Requests/DemoDepositRequest.cs`
- `backend/tests/FinCore.Application.Tests/DemoDepositHandlerTests.cs`
- `backend/tests/FinCore.IntegrationTests/DemoDepositTests.cs`
- `backend/tests/FinCore.IntegrationTests/DemoDepositEnvironmentTests.cs`
- `docs/demo-deposit.md`

Updated:

- `backend/src/FinCore.Application/Abstractions/Persistence/IWalletRepository.cs`
- `backend/src/FinCore.Infrastructure/Persistence/Repositories/WalletRepository.cs`
- `backend/src/FinCore.Api/Controllers/WalletsController.cs`
- `backend/src/FinCore.Api/Program.cs`
- `backend/src/FinCore.Api/appsettings.Development.json`
- `backend/src/FinCore.Api/FinCore.Api.http`
- `README.md`

The existing middleware, Domain entities, migrations, base appsettings credentials,
and repository/context DI registrations were not changed by this feature.
