# Finance Agent Platform v1.1

Two Fastify TypeScript services for the VaultCoreLite banking core:

1. `apps/agent-service` — Vercel AI SDK swarm-like finance agent using Gemini through `@ai-sdk/google`.
2. `apps/tool-gateway` — policy gateway that wraps VaultCoreLite C# v1.2 and enforces auth, ownership, allow-lists, simulation-first confirmation, and safe errors.

The core rule is:

```text
LLM decides / explains / plans.
VaultCoreLite validates / simulates / executes / audits.
The LLM never mutates money directly.
```

## Architecture

```text
User / Chat UI
  ↓
Fastify Agent Service + Vercel AI SDK
  ↓
Fastify Agent Tool Gateway
  ↓
VaultCoreLite C# API v1.2
  ↓
Immutable ledger + product plugins + simulation confirmation audit
```

## What changed in v1.1

- Switched from `pnpm` to **npm workspaces**.
- Imported the best patterns from the reference Fastify agent app: typed env loading, dependency-injected app construction, model role separation, route-scoped rate limiting, Swagger/Zod setup, and graceful shutdown.
- Restored the useful specialist finance agents instead of keeping a small conservative set.
- Added advanced deterministic tools for portfolio risk, investment plan simulation, retirement runway, tax scenarios, mortgage refinance, subscription bills, emergency fund gap, credit utilization, FX/cash management support hooks.
- Removed hard-coded “US-specific” routing. This repo is configured for Israel-oriented deployments (`IL`, `ILS/USD/EUR`) and uses deployment policy to enable/disable advanced finance modules.
- Still keeps the critical money safety invariant: raw execution is blocked; only `human confirmation endpoint` can execute a stored VaultCoreLite simulation.

## Quick start

```bash
cp .env.example .env
npm install
npm run dev:gateway
npm run dev:agent
```

Mock mode is enabled in `.env.example` so you can run the agent/gateway before the C# core is running:

```env
BANK_CORE_MOCK_MODE=true
```

When VaultCoreLite C# is running:

```env
BANK_CORE_MOCK_MODE=false
BANK_CORE_BASE_URL=http://localhost:5080
BANK_CORE_TOKEN=dev-core-token
```

## Services

### Tool Gateway

Runs on `http://localhost:4020`.

Read tools:

- `get_account_balance`
- `get_recent_transactions`
- `get_financial_health_snapshot`
- `analyze_portfolio_risk`
- `analyze_subscription_bills`

Simulation tools:

- `simulate_transaction`
- `simulate_savings_plan`
- `simulate_loan_payoff`
- `simulate_cashflow_forecast`
- `simulate_emergency_fund_gap`
- `simulate_investment_plan`
- `simulate_retirement_runway`
- `simulate_tax_scenario`
- `simulate_mortgage_refinance`
- `simulate_credit_utilization_strategy`

Execution tool:

- `human confirmation endpoint` — the only execution-capable path; no confirmation token is exposed to the model.

Direct tools such as `execute_transfer`, `buy_security`, or `rebalance_portfolio` are intentionally blocked and return `REQUIRES_CONFIRMATION`.

### Agent Service

Runs on `http://localhost:4010`.

```bash
curl -X POST http://localhost:4010/v1/chat \
  -H 'Content-Type: application/json' \
  -d '{
    "language":"he",
    "userId":"u1",
    "customerId":"c1",
    "accountIds":["11111111-1111-1111-1111-111111111111"],
    "message":"יש לי 1200 שקל פנויים החודש. מה כדאי לעשות?"
  }'
```

## Swarm agents

- `triage`
- `daily_money_copilot`
- `savings_goal_optimizer`
- `debt_payoff_strategist`
- `family_finance_manager`
- `personal_wealth_advisor`
- `portfolio_risk_manager`
- `retirement_runway_modeler`
- `tax_optimization_strategist`
- `mortgage_loan_broker`
- `subscription_bill_negotiator`
- `credit_score_optimizer`
- `freelancer_cashflow_manager`
- `israel_fx_cash_manager`
- `kyc_aml_assistant`
- `investment_research_buffett`
- `investment_research_lynch`
- `investment_research_macro`
- `confirmation_specialist`

Handoffs are implemented with normal Vercel AI SDK tools that return `{ _handoffTo: ... }`. The runtime detects the payload, swaps the active agent prompt/toolset, and continues with a typed state payload.

## Audience

This deployment is configured for Israel-oriented users in Hebrew or English with `ILS`, `USD`, and `EUR`. Advanced finance modules are controlled by deployment policy via `ENABLE_ADVANCED_FINANCE_TOOLS=true`.

## Verification

See `docs/VERIFY.md`.
