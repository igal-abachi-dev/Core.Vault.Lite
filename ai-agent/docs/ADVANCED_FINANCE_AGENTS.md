# Advanced Finance Agents

v1.1 restores a broad set of specialist agents so the system is useful beyond basic budgeting.

## Personal finance

- Daily Money Co-Pilot
- Smart Savings & Goal Optimizer
- Debt & Loan Payoff Strategist
- Family / Shared Finance Manager
- Freelancer Cashflow Manager
- Subscription & Bill Negotiator
- Credit Score Optimizer

## Wealth and investing

- Personal Wealth Advisor
- Portfolio Risk Manager
- Retirement Runway Modeler
- Buffett Intrinsic Value Gatekeeper
- Peter Lynch Consumer Trend Scout
- Dalio Macro Regime Modeler

## Operations and specialty finance

- Tax Optimization Strategist
- Mortgage & Loan Broker
- Israel FX Cash Manager
- KYC / AML Assistant

## Execution boundary

These agents can use powerful deterministic analysis and simulation tools. They do not receive raw direct execution tools. Any mutation must use the VaultCoreLite v1.2 simulation confirmation path:

```text
simulate_* -> preview -> user confirms -> UI human confirmation endpoint -> core audit
```

This is not a usefulness limitation. It is the execution protocol that keeps the system auditable and prevents the model from inventing a different action at the last moment.
