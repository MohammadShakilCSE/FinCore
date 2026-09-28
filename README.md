# FinCore

The [React frontend](fincore-web/README.md) includes the login page connected to the backend authentication API. Run it with `npm install` and `npm run dev` from `fincore-web`.

FinCore is a fintech backend built with ASP.NET Core and .NET 10 using Clean Architecture.
Wallet creation and lookup now persist to PostgreSQL through EF Core.

Read the [PostgreSQL persistence lesson](docs/postgresql-persistence.md) for setup,
migration commands, tests, request flow, mapping decisions, and beginner-friendly explanations.
See [architecture](docs/architecture.md) for layer responsibilities.

The [Demo Deposit lesson](docs/demo-deposit.md) explains simulated wallet deposits,
development/test restrictions, tracked updates, verification, and concurrency limitations.

See [authentication](docs/authentication.md) for JWT setup, customer wallet ownership, and HTTP examples. Public demo deposits are now disabled; this supersedes the earlier demo-deposit endpoint instructions.
