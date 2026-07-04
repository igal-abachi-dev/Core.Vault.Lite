# Agent Flows

## Daily money co-pilot

```text
triage -> daily_money_copilot
  -> get_financial_health_snapshot
  -> get_account_balance
  -> get_recent_transactions
  -> simulate_cashflow_forecast
  -> explain risks/options
```

## Extra cash allocation

```text
triage -> savings_goal_optimizer
  -> get_financial_health_snapshot
  -> simulate_emergency_fund_gap
  -> simulate_savings_plan
  -> simulate_loan_payoff
  -> simulate_investment_plan
  -> explain conservative/balanced/aggressive options
```

## Wealth/investment swarm

```text
triage -> personal_wealth_advisor
  -> portfolio_risk_manager
  -> investment_research_lynch / investment_research_buffett / investment_research_macro
  -> simulate_investment_plan
  -> simulate_retirement_runway
  -> explain scenario outputs
```

## Confirmation

```text
agent proposes action
  -> simulation tool returns simulationId + confirmationToken
  -> user explicitly confirms
  -> triage routes to confirmation_specialist
  -> confirm_simulation
  -> VaultCoreLite executes stored simulation request
```

## Loop protection

The swarm runner stops after `MAX_HANDOFFS` and asks the user to clarify a single next action. Agents pass typed summaries only, not full raw histories.
