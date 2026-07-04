# Tool Safety Model

The agent service never calls VaultCoreLite directly. It calls the Agent Tool Gateway.

## Risk levels

| Risk | Tools | Confirmation |
|---|---|---|
| read | `get_*`, `analyze_*` | No |
| simulate | `simulate_*` | No mutation; returns preview |
| execute | `human confirmation endpoint` | Requires token + idempotency key |

## Execution rule

Raw execution tools are blocked:

- `execute_transfer`
- `execute_deposit`
- `execute_withdrawal`
- `create_bank_account`
- `schedule_recurring_payment`
- `open_term_deposit`
- `buy_security`
- `sell_security`
- `rebalance_portfolio`

The only execution path is:

```text
simulate_* -> user sees preview -> user confirms -> human confirmation endpoint -> VaultCoreLite executes stored request
```

This prevents the LLM from simulating one action and executing another.

## Gateway checks

- Bearer token required between agent and gateway.
- User/account context supplied through trusted request body or headers.
- Supported country is `IL`.
- Supported currencies are `ILS`, `USD`, `EUR`.
- Account allow-list is enforced when provided.
- Execution amount limits are enforced in policy.
- Advanced finance tool access is controlled by deployment config.
- Errors are redacted before returning to the agent.
