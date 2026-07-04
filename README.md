# Core.Vault.Lite
C#/.NET Banking Core Engine focused on the essential architecture: immutable postings, double-entry accounting, product-contract hooks, schedule-driven product lifecycle events, real-time balance projections, and an outbox event stream.

## Architecture

- ASP.NET Core Minimal API on Kestrel.
- Application command/service layer.
- Domain ledger engine with immutable postings and deterministic double-entry expansion.
- EF Core + Npgsql PostgreSQL persistence.
- Purpose-built repositories + Unit of Work. No generic CRUD on the money path.
- Product logic behind `IProductRuntime`.
- Runtime mode is config-driven: `Stub` for local dev, or trusted compiled plugins loaded with `AssemblyLoadContext`.
- Cronos-based scheduler plus hosted background worker.
- Schedule-run table for exactly-once-per-due-time claims across multiple API instances.
- Transactional outbox for ledger, batch, and client-transaction events.

## Product library included

`plugins/BankProducts.Plugin` contains essential bank product contracts:

- `CurrentAccount` — current/checking account with overdraft limit, transaction fee, monthly fee.
- `SavingsAccount` — no overdraft/min-balance account, daily accrual and interest application hooks.
- `TermDeposit` — maturity-locking and maturity interest.
- `Wallet` — prepaid wallet, no negative balance.
- `PersonalLoan` — asset-side loan with principal limit and daily interest accrual.
- `MortgageLoan` — asset-side mortgage-style loan with the same core interest engine.
- `CreditCard` — asset-side revolving credit with credit limit, annual fee, and interest accrual.

These are intentionally compact product-contract examples, not jurisdiction-specific regulated product packs.


## agentic flow:
Agent proposes money action
  ↓
Core simulates it without committing postings
  ↓
Core returns preview + one-time confirmation token
  ↓
User explicitly confirms
  ↓
Core executes the stored request, not a new model-generated request
  ↓
Core writes immutable ledger + confirmation audit + outbox events


## Run locally

```bash
docker compose up -d postgres
export ConnectionStrings__VaultDb='Host=localhost;Port=5432;Database=vaultlite;Username=vaultlite_app;Password=vaultlite_app'
dotnet tool restore || true
dotnet ef database update --project src/VaultCoreLite.Infrastructure --startup-project src/VaultCoreLite.Api
dotnet run --project src/VaultCoreLite.Api
```

## Plugin mode

Build the product-library plugin, copy it into the API output `plugins/` folder, then set:

```json
{
  "ProductRuntime": {
    "Mode": "Plugin",
    "PluginPath": "plugins/BankProducts.Plugin.dll",
    "RequireAuthenticode": false
  }
}
```

For production Windows deployments, sign plugin DLLs and set `RequireAuthenticode=true`. The loader verifies the plugin before loading when this flag is enabled.

## Scheduler

The scheduler supports arbitrary Cronos cron expressions through:

```http
POST /v1/schedules
```

and the compatibility helper:

```http
POST /v1/schedules/daily
```

The hosted `SchedulerBackgroundService` polls due schedules. You can disable or tune it:

```json
{
  "SchedulerWorker": {
    "Enabled": true,
    "PollIntervalSeconds": 15,
    "BatchSize": 100
  }
}
```

Manual trigger remains available:

```http
POST /v1/scheduler/run-due
```

## EF migrations

```bash
dotnet ef migrations add InitialLedgerSchema \
  --project src/VaultCoreLite.Infrastructure \
  --startup-project src/VaultCoreLite.Api

dotnet ef database update \
  --project src/VaultCoreLite.Infrastructure \
  --startup-project src/VaultCoreLite.Api
```



## v1.2 Agent-safe execution

v1.2 adds simulation-first, confirmation-gated money execution for AI agent use cases:

- `POST /v1/simulations/transaction`
- `GET /v1/simulations/{id}`
- `POST /v1/simulations/{id}:confirm`

The LLM/agent should call simulation tools first. The core returns a preview plus one-time confirmation token. Execution reuses the stored request and the posting engine's idempotency key, then records confirmation audit and outbox events.

See `docs/V1_2_AGENT_SAFE_EXECUTION.md`.
