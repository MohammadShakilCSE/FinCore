# Wallet optimistic concurrency

Optimistic concurrency means letting requests work independently, then checking at save
time that the data has not changed since it was read. FinCore now rejects stale Wallet
writes instead of silently replacing a newer balance or status.

## Version and the aggregate

Wallet.Version starts at 1. Successful Credit, Debit, Suspend, Activate, and Close
operations increment it once when they change state. Validation failures do not increment
it. Activate on an already active wallet and Close on an already suspended zero-balance
wallet remain no-ops. Existing lifecycle rules are preserved: Close still uses Suspended,
and Activate still rejects Suspended. No Closed enum member was introduced.

Version belongs to Wallet because it describes the whole aggregate: the balance and
status are state controlled by that root. A status change must invalidate a stale credit
just as a debit does. The setter is private; handlers never assign Version. A request
can make several valid changes before saving, advancing Version several times. EF still
compares against the original version it read, not an assumed current-minus-one value.

## How EF detects a stale write

WalletConfiguration marks Version with IsConcurrencyToken(). This tells EF to include
the original tracked version in its update condition, alongside the wallet ID:

```sql
UPDATE "Wallets"
SET "Balance" = @balance, "Version" = @newVersion
WHERE "Id" = @id AND "Version" = @originalVersion;
```

EF generates the SQL; the application does not execute a hand-written update. Money's
existing owned mapping remains in the Wallets row, so the balance update participates
in this version check.

For a migrated wallet holding 1000 at Version 1, requests A and B may both read Version 1.
A debits 800 and saves balance 200, Version 2. B's stale debit of 700 tries to match Version
1; no row matches. EF throws DbUpdateConcurrencyException. B cannot overwrite A's 200.

Integration tests seed wallets through Create and Credit, so their funded starting
version is 2 and the successful debit advances it to 3. This is the same stale-write
scenario; resetting Version for test convenience would bypass the Domain behavior.

## Exception translation and HTTP

FinCoreDbContext catches DbUpdateConcurrencyException at both its asynchronous and
synchronous save boundaries and throws Application's ConcurrencyConflictException,
preserving the original as InnerException. Other database exceptions propagate normally.
The parameterless/ordinary save overloads flow through these overridden methods too.

Application therefore does not reference EF. The existing global API middleware returns
HTTP 409 with a safe refresh-and-retry message, never the database exception details.
This already benefits Demo Deposit; there is still no public transfer endpoint.
There is no automatic retry. Discard a failed operation's scope and tracked entities;
a future user-initiated attempt must reload current data and revalidate business rules.

## One atomic transfer commit

Both repositories remain scoped and receive the same scoped DbContext. Both wallets are
tracked, and LedgerTransactionRepository.AddAsync only stages the transaction graph.
TransferMoneyHandler calls SaveChangesAsync once after debiting, crediting, and staging
the balanced ledger.

Atomic persistence means the database commits the whole save or rolls it back. If the
sender or receiver is stale, PostgreSQL must not retain a receiver credit or ledger
record from that failed transfer. Tests verify these persisted outcomes using a fresh
context after the conflict. No manual transaction or independent transfer save was added.

WalletRepository.AddAsync retains its existing immediate-save behavior for Create Wallet;
the transfer does not call it. All existing save paths pass through the central exception
translation. Searches found the only application balance writers are DemoDepositHandler
and TransferMoneyHandler, both using Domain Credit/Debit. Status writers are the Wallet
methods. There are no current background wallet writers. Direct SQL or future bulk
writers must explicitly respect this scheme; an EF token does not police arbitrary SQL.

## Migration and verification

`20260919190005_AddWalletConcurrencyVersion` adds only the non-null integer Version column
to Wallets. Its migration default was reviewed and changed from EF's generated 0 to 1
for existing rows. The model remains application-managed, not a SQL Server rowversion
or PostgreSQL-generated token. Wallets were not recreated, and ledger tables were not altered.

Applied successfully to the configured **localhost:5432/fincore** development database.
The existing row now has Version 1. Full-row fingerprints excluding the new column
matched before and after migration, confirming all existing wallet data was preserved.
The model snapshot has no pending changes.

Validation completed:

- Build: **0 warnings, 0 errors**.
- Full suite: **106 passed, 0 failed, 0 skipped** (31 Domain, 27 Application, 48 in IntegrationTests).
- Thirteen new Domain cases cover successful/failed state changes and no-ops.
- Five new PostgreSQL cases cover stale debit, competing complete transfers, deposit
  versus transfer in both winning orders, and a status change versus stale credit.
- The transfer test resolves repositories and handlers in two real DI scopes, verifying
  the staged ledger uses each scope's wallet context. It checks the losing receiver and
  ledger are unchanged, the winner's entries balance, and the total remains 1000.
- One middleware test checks safe 409 output without leaking inner exception details.

Concurrency tests explicitly load the same wallet in two contexts before the first
save; they use no sleeps or timing races. They ran on real PostgreSQL using the existing
temporary test-database fixture, not EF InMemory.

Commands from repository root:

```powershell
dotnet build backend/FinCore.sln --no-restore
# Already generated; do not add it again:
dotnet ef migrations add AddWalletConcurrencyVersion --project backend/src/FinCore.Infrastructure --startup-project backend/src/FinCore.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update AddWalletConcurrencyVersion --project backend/src/FinCore.Infrastructure --startup-project backend/src/FinCore.Infrastructure --no-build
$settings = Get-Content backend/src/FinCore.Api/appsettings.json -Raw | ConvertFrom-Json
$env:FinCore_TestConnectionString = $settings.ConnectionStrings.FinCore
dotnet test backend/FinCore.sln --no-build --no-restore
```

Infrastructure remains the startup project for migration tooling because its existing
design-time factory reads the API appsettings. No connection configuration was changed.

## Changed files

- `backend/src/FinCore.Domain/Entities/Wallet.cs`: Version and increments.
- `backend/src/FinCore.Infrastructure/Persistence/Configurations/WalletConfiguration.cs`: concurrency token.
- `backend/src/FinCore.Infrastructure/Persistence/Context/FinCoreDbContext.cs`: exception translation.
- `backend/src/FinCore.Application/Exceptions/ConcurrencyConflictException.cs`: framework-independent conflict.
- `backend/src/FinCore.Api/Middleware/ExceptionHandlingMiddleware.cs.cs`: safe 409 response.
- `backend/src/FinCore.Infrastructure/Persistence/Migrations/20260919190005_AddWalletConcurrencyVersion.cs` and its Designer: migration.
- `backend/src/FinCore.Infrastructure/Persistence/Migrations/FinCoreDbContextModelSnapshot.cs`: updated model.
- `backend/tests/FinCore.Domain.Tests/Entities/WalletVersionTests.cs`: state-version tests.
- `backend/tests/FinCore.IntegrationTests/WalletConcurrencyTests.cs`: PostgreSQL conflict/rollback tests.
- `backend/tests/FinCore.IntegrationTests/ConcurrencyMiddlewareTests.cs`: HTTP conflict mapping test.
- `docs/wallet-concurrency.md`, `docs/demo-deposit.md`, `docs/transfer-application.md`: lesson and progress notes.

This protects stale competing updates, not repeated logical requests. Idempotency and
duplicate-request protection remain unimplemented. Existing demo deposits are not
automatically ledger-backed, and no historical reconciliation or public transfer API
was added. This remains an educational simulated-money project.
