# Transfer application layer

Current progress: [Wallet optimistic concurrency](wallet-concurrency.md) now adds version
checks and PostgreSQL conflict/rollback tests. The deferred concurrency/testing work
below describes this earlier lesson; idempotency and the public API remain pending.

This adds an internal simulated-money use case. There is no public transfer endpoint,
schema migration, or change to Wallet business rules.

## Workflow

`TransferMoneyCommand` is a plain record describing intent: which wallet sends, which
receives, how much, and in which currency. `TransferMoneyHandler` coordinates these steps:

1. Reject empty wallet IDs and self-transfers before loading data.
2. Load both wallets through `GetTrackedByIdAsync`; missing wallets raise DomainException.
3. Construct Money. Its existing validation handles negative values, currency format,
   and supported precision/range.
4. Call sender.Debit and receiver.Credit. Wallet enforces activity, matching currency,
   positive amounts, and sufficient sender funds.
5. Call LedgerTransaction.CreateTransfer. The existing aggregate creates and validates
   the balanced debit/credit entries representing this movement.
6. Stage the aggregate through ILedgerTransactionRepository.AddAsync.
7. Await IWalletRepository.SaveChangesAsync exactly once, then return the transaction
   ID, reference, normalized currency, and amount.

A ledger transaction is a record of the movement with its related entry lines; an
aggregate is a group whose rules are controlled through its root object. Here the root
is LedgerTransaction, which creates and balances its entries. Recording these lines
does not reconcile old demo deposits or turn them into historical ledger records.

## Why repositories and dependency injection

The repository pattern gives Application a small persistence contract, so it does not
need SQL or EF types. ILedgerTransactionRepository lives in Application because the
use case defines what it needs. LedgerTransactionRepository lives in Infrastructure
because it implements that need using EF Core.

Its AddAsync calls context.LedgerTransactions.AddAsync, tracking the entire new aggregate,
including child entries and owned Money values. It never saves. This differs deliberately
from the existing WalletRepository.AddAsync, which still commits wallet creation.
The transfer does not use that wallet-creation method.

Dependency injection means supplying dependencies through constructors rather than
creating them inside the handler. Infrastructure registers both repositories as scoped;
AddDbContext also registers a scoped context. Within one DI scope (normally one HTTP
request), both repositories therefore receive the same FinCoreDbContext. Program registers
TransferMoneyHandler as scoped alongside the existing handlers. Do not manually construct
repositories with separate contexts for one transfer.

These small abstractions preserve the existing layer boundaries, but repositories add
indirection over EF. No generic repository, new coordination service, or additional Unit
of Work abstraction is needed for this step.

## Tracking, one save, and failure behavior

Tracking means EF remembers loaded objects and detects changes to them. Both wallets
must be tracked so changing them through Domain methods becomes database updates.
The read-only Get Wallet method still uses AsNoTracking and is unchanged.

The shared context holds both modified wallets and the new ledger graph. Its single
SaveChangesAsync uses EF's default PostgreSQL transaction behavior to commit the commands
together, or roll them back if a command fails. Atomic means all these database changes
succeed together or none of them do. The current Task-returning SaveChangesAsync contract
already supports this; no signature change or separate sender/receiver save was needed.

Database atomicity does not undo in-memory Domain operations. If receiver.Credit fails
after sender.Debit succeeds, nothing is saved but the tracked sender has changed. End and
dispose that operation's scope on failure; do not catch the exception and later save or
retry using the same tracked objects. A staging or save error also propagates rather
than returning a successful result.

## Tests and verification

The new Application tests use handwritten repository substitutes, matching the existing
test style. They capture the ledger passed to AddAsync and inspect its two entries,
wallet IDs, amounts, currency, status, and parent IDs. They verify balance changes,
load/stage/save order, one save on success, no save or ledger addition on validation
failure, and cancellation-token forwarding.

Failure cases cover empty IDs, missing wallets, self-transfer, insufficient funds,
sender and receiver currency mismatches, zero/negative amounts, both wallets suspended
or closed through existing Domain methods, receiver overflow, and repository failures.
Close currently produces Suspended; the lifecycle implementation is unchanged.

Commands from the repository root:

```powershell
dotnet build backend/FinCore.sln --no-restore
$settings = Get-Content backend/src/FinCore.Api/appsettings.json -Raw | ConvertFrom-Json
$env:FinCore_TestConnectionString = $settings.ConnectionStrings.FinCore
dotnet test backend/FinCore.sln --no-build --no-restore
```

Results: build **0 warnings, 0 errors**; **87 tests passed, 0 skipped** (18 Domain,
27 Application, 42 existing IntegrationTests). Eighteen Application cases are new.
Existing PostgreSQL tests used their own temporary databases on the configured local
server. The development host also started successfully with DI validation enabled by
the normal Development defaults. No transfer was executed against the development database.

Unit tests do not establish real database atomicity or concurrency safety. New PostgreSQL
transfer integration tests are intentionally deferred to the next step.

## Before a public API

Two requests can still read the same sender balance and both spend it. A single save
does not detect that race. Database-level concurrency protection is still needed.
Idempotency means retrying one logical request does not execute it twice; idempotency
keys and duplicate-request protection are also pending. Add real PostgreSQL transfer
success/rollback tests and reconcile existing Demo Deposit balances with the ledger
before exposing a public endpoint. This educational feature is not production-ready.

## Files

Created:

- `backend/src/FinCore.Application/Abstractions/Persistence/ILedgerTransactionRepository.cs`
- `backend/src/FinCore.Infrastructure/Persistence/Repositories/LedgerTransactionRepository.cs`
- `backend/src/FinCore.Application/Features/Transfers/TransferMoney/TransferMoneyCommand.cs`
- `backend/src/FinCore.Application/Features/Transfers/TransferMoney/TransferMoneyResult.cs`
- `backend/src/FinCore.Application/Features/Transfers/TransferMoney/TransferMoneyHandler.cs`
- `backend/tests/FinCore.Application.Tests/TransferMoneyHandlerTests.cs`
- `docs/transfer-application.md`

Modified:

- `backend/src/FinCore.Infrastructure/DependencyInjection.cs`: scoped ledger repository registration.
- `backend/src/FinCore.Api/Program.cs`: scoped transfer handler registration.
