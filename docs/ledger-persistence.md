# Ledger persistence

Ledger persistence is configured in Infrastructure using the existing EF Core/Npgsql
packages and configuration discovery. No new endpoint, transfer handler, repository,
concurrency feature, or idempotency behavior is introduced.

## How the mapping works

- `LedgerTransactions` stores the aggregate's identity, reference, status, and creation
  time. `LedgerEntries` stores its individual debit/credit lines. Separate tables allow
  one transaction to contain several entries without repeating transaction metadata.
- Every entry requires a `LedgerTransactionId` referencing its parent. This is the
  one-to-many relationship. An index supports parent-based lookups. `ON DELETE RESTRICT`
  prevents deleting a transaction while entries still reference it; it does not make
  the entire ledger append-only or prevent directly deleting entries.
- The `Entries` navigation explicitly uses `_entries` with field access. Loading via
  `Include(transaction => transaction.Entries)` lets EF populate the private list while
  callers still receive the read-only collection. EF does not automatically load the
  entries on every query; callers must request them when needed.
- `Money` is owned by each entry and stored in the same row as `Amount numeric(19,4)`
  and `Currency varchar(3)`. Both are required. Check constraints enforce positive
  amounts and exactly three uppercase ASCII currency letters. There is no Money table.
- Transaction references are required, limited to 64 characters, and unique. Enum
  values use strings of up to 16 characters. Both entity IDs are generated in Domain,
  not by PostgreSQL. Inherited creation timestamps use `timestamp with time zone`.
- `WalletId` is a required scalar, as requested; no additional Wallet foreign key was
  introduced. Balanced totals and transaction lifecycle remain Domain rules, not
  cross-row database checks.

## Compatibility corrections

The current Domain renamed Money to `FinCore.Domain.ValueObjects.Money`. Five stale
using directives in the existing handler/tests were updated. The generated snapshot
also records the new type name; the migration makes no Wallet table changes. Historical
migrations were left intact.

Originally both entries shared the same Money object. A real PostgreSQL test reproduced
an EF owned-type failure: one entry was inserted without an amount. `LedgerEntry.Create`
now constructs an equal, separate Money instance for each entry. This is a minimal
mapping compatibility correction, with no EF dependency or business-rule change in
Domain. The existing aggregate factory and private collection remain intact.

The WalletStatus enum, mapping, and prior migration all use Active, Inactive, Suspended.
No status correction was necessary. The existing Close() method still uses Suspended;
this task neither substitutes meanings nor adds a Closed state.

## Migration and validation

Migration: `20260919183853_AddLedgerTables`.
Applied successfully to the configured **localhost:5432/fincore** development database.
The migration creates only the two ledger tables, checks, relationship, and indexes.
Before/after full-row fingerprints confirmed the one existing Wallet row was unchanged.
Direct psql schema inspection confirmed the result and the migration history entry.
The ledger tables remain empty in the development database: no historical records were
invented, and Demo Deposit balances are not reconciled by this change.

Build: **0 warnings, 0 errors**. Tests: **69 passed, 0 skipped** (18 Domain, 9 Application,
42 in IntegrationTests). Ten new real PostgreSQL cases cover fresh-context loading of
the private collection and immutable Money, rejected invalid amounts/currencies/orphan
entries, unique references, and restricted parent deletion. Test databases are temporary
and separate from the development database. EF reports no pending model changes.

Commands from the repository root:

```powershell
dotnet build backend/FinCore.sln --no-restore
dotnet ef migrations add AddLedgerTables --project backend/src/FinCore.Infrastructure --startup-project backend/src/FinCore.Infrastructure --output-dir Persistence/Migrations
# Migration already exists: do not generate it again.
dotnet ef database update AddLedgerTables --project backend/src/FinCore.Infrastructure --startup-project backend/src/FinCore.Infrastructure --no-build
$settings = Get-Content backend/src/FinCore.Api/appsettings.json -Raw | ConvertFrom-Json
$env:FinCore_TestConnectionString = $settings.ConnectionStrings.FinCore
dotnet test backend/FinCore.sln --no-build --no-restore
```

Infrastructure is both migration target and startup project because this solution already
has a design-time factory there. It reads API's appsettings.json. The test-only variable
is consumed by the existing PostgreSQL fixture; application configuration is unchanged.

## Files changed in this task

Created:

- `backend/src/FinCore.Infrastructure/Persistence/Configurations/LedgerTransactionConfiguration.cs`
- `backend/src/FinCore.Infrastructure/Persistence/Configurations/LedgerEntryConfiguration.cs`
- `backend/src/FinCore.Infrastructure/Persistence/Migrations/20260919183853_AddLedgerTables.cs`
- `backend/src/FinCore.Infrastructure/Persistence/Migrations/20260919183853_AddLedgerTables.Designer.cs`
- `backend/tests/FinCore.IntegrationTests/LedgerPersistenceTests.cs`
- `docs/ledger-persistence.md`

Modified:

- `backend/src/FinCore.Infrastructure/Persistence/Context/FinCoreDbContext.cs`: two additional DbSets.
- `backend/src/FinCore.Infrastructure/Persistence/Migrations/FinCoreDbContextModelSnapshot.cs`: generated model.
- `backend/src/FinCore.Domain/Entities/LedgerEntry.cs`: independent Money instances.
- `backend/src/FinCore.Application/Features/Wallets/DemoDeposit/DemoDepositHandler.cs`: corrected Money import only.
- `backend/tests/FinCore.Application.Tests/DemoDepositHandlerTests.cs`: corrected Money import only.
- `backend/tests/FinCore.Domain.Tests/Entities/MoneyTests.cs`: corrected Money import only.
- `backend/tests/FinCore.Domain.Tests/Entities/WalletTests.cs`: corrected Money import only.
- `backend/tests/FinCore.IntegrationTests/DemoDepositTests.cs`: corrected Money import only.
