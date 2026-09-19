# FinCore Architecture

Dependencies point inward: API composes Application and Infrastructure; Infrastructure
implements Application persistence contracts; Application uses Domain. Domain has no
references to EF Core or other projects.

- **Domain:** Wallet rules and immutable Money; validates currency format and amount range/scale.
- **Application:** CreateWalletHandler and GetWalletHandler; repository interface and result records.
- **Infrastructure:** EF Core/Npgsql context, fluent mappings, PostgreSQL repository, migrations, and DI registration.
- **API:** Controller routing, request/response handling, configuration, and composition.

WalletRepository.AddAsync commits immediately. GetByIdAsync is a no-tracking read.
DbContext and repository have scoped lifetimes. We deliberately do not introduce a new
Unit of Work abstraction for this single-write use case. Multi-wallet writes and concurrency
would need explicit design before adding transfer endpoints.

POST /api/wallets returns 201 with Location; GET /api/wallets/{id} returns 200 or 404.
Domain creation failures return 400. Migrations run explicitly, not during API startup.

See the [persistence lesson](postgresql-persistence.md) for the full rationale and commands.
