# Core.Vault.Lite - for agentic finance/banking/investing
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


--

User / Chat UI
  ↓
Fastify Agent Service
  ↓
Vercel AI SDK tool calling / swarm handoffs
  ↓
Fastify Agent Tool Gateway
  ↓
Core.Vault.Lite C# API
  ↓
Simulation-first confirmed execution + immutable audit

---

use cases:
Daily money co-pilot	Immediate user value, low regulatory risk,

Smart savings optimizer	Uses simulations + schedules + products,

Debt payoff strategist	High value, mostly deterministic math,

Family/shared finance manager	Great differentiation,

Retirement projection	Useful but needs careful assumptions,

Investing assistant	Powerful, but legally sensitive,

and more ...

```bash
finance-agent-platform/
├── apps/
│   ├── agent-service/       # Vercel AI SDK + Gemini swarm-like agent
│   └── tool-gateway/        # safe gateway wrapping Core.Vault.Lite api
├── packages/
│   ├── schemas/             # shared Zod schemas
│   ├── agent-policy/        # risk classification + safety policy
│   └── banking-tools/       # Vercel AI SDK tool wrappers
└── docs/
```
swarm agents: for example
triage

daily_money_copilot

savings_goal_optimizer

debt_payoff_strategist

family_finance_manager

investment_education_analyst

confirmation_specialist

```json
export function createHandoffTools() {
  return {
    transfer_to_daily_money_copilot: handoffTool('daily_money_copilot', 'Use for budgeting, spending, balances, overdraft, cashflow, and everyday money management.'),
    transfer_to_savings_goal_optimizer: handoffTool('savings_goal_optimizer', 'Use for extra cash, emergency fund, savings goals, term deposits, and allocation planning.'),
    transfer_to_debt_payoff_strategist: handoffTool('debt_payoff_strategist', 'Use for loans, credit cards, mortgage payoff, refinancing comparison, and repayment strategy.'),
    transfer_to_family_finance_manager: handoffTool('family_finance_manager', 'Use for family, spouse, joint accounts, children savings, allowances, and shared goals.'),
    transfer_to_personal_wealth_advisor: handoffTool('personal_wealth_advisor', 'Use for broad wealth allocation across cash, debt, savings, retirement and investment simulations.'),
    transfer_to_portfolio_risk_manager: handoffTool('portfolio_risk_manager', 'Use for portfolio concentration, risk, asset allocation, currency exposure and rebalancing simulation.'),
    transfer_to_retirement_runway_modeler: handoffTool('retirement_runway_modeler', 'Use for long-horizon retirement runway, withdrawals, inflation and contribution analysis.'),
    transfer_to_tax_optimization_strategist: handoffTool('tax_optimization_strategist', 'Use for tax scenario modeling, deductions, timing of gains/losses, and set-aside planning.'),
    transfer_to_mortgage_loan_broker: handoffTool('mortgage_loan_broker', 'Use for mortgage refinancing, extra payments, balance transfers and loan comparison.'),
    transfer_to_subscription_bill_negotiator: handoffTool('subscription_bill_negotiator', 'Use for recurring subscriptions, bills, duplicate expenses and cancellation/negotiation opportunities.'),
    transfer_to_credit_score_optimizer: handoffTool('credit_score_optimizer', 'Use for credit utilization, card payoff strategy and credit-building simulations.'),
    transfer_to_freelancer_cashflow_manager: handoffTool('freelancer_cashflow_manager', 'Use for irregular income, tax set-aside, invoice/cashflow and self-employed planning.'),
    transfer_to_israel_fx_cash_manager: handoffTool('israel_fx_cash_manager', 'Use for ILS/USD/EUR cash exposure, travel/tuition/mortgage FX planning and currency risk.'),
    transfer_to_kyc_aml_assistant: handoffTool('kyc_aml_assistant', 'Use for operational KYC/AML summaries, missing documents and unusual-activity review assistance.'),
    transfer_to_buffett_gatekeeper: handoffTool('investment_research_buffett', 'Use for intrinsic value, moat, owner earnings and margin-of-safety research.'),
    transfer_to_lynch_scout: handoffTool('investment_research_lynch', 'Use for consumer trend, GARP and everyday product adoption investment research.'),
    transfer_to_macro_modeler: handoffTool('investment_research_macro', 'Use for macro regime, inflation/rates/currency and all-weather allocation scenario analysis.'),
    transfer_to_confirmation_specialist: handoffTool('confirmation_specialist', 'Use only when the user explicitly provides a simulationId and confirmationToken and asks to execute/confirm.'),
  };
}
```
--

The gateway exposes the safe tool split:

Read:
- get_account_balance
- get_recent_transactions
- get_financial_health_snapshot

Simulation:
- simulate_transaction
- simulate_savings_plan
- simulate_loan_payoff
- simulate_cashflow_forecast

Execution:
- confirm_simulation only
- 

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

## v1.3 safety hardening note

This bundle includes the confirmation hardening pass from the senior review:

- `MoneySimulation` confirmation now uses a `FOR UPDATE` row lock.
- `Confirmed` simulations are reconciled after crash/retry: if the batch exists, the simulation is marked `Executed`; if it does not, the stored `BatchRequest` is safely replayed through the idempotent posting engine.
- The AI agent no longer receives or handles confirmation tokens.
- The Tool Gateway captures confirmation tokens out-of-band and exposes a UI-only endpoint: `POST /v1/human-confirmations/{simulationId}/confirm`.
- The agent service returns `pendingConfirmations[]` metadata so a UI can render a confirmation button without leaking a token into model context.

See:

- `docs/V1_3_CONFIRMATION_HARDENING.md`
- `ai-agent/docs/HUMAN_CONFIRMATION_FLOW.md`
