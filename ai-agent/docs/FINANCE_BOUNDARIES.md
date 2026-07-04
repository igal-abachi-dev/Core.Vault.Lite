# Finance Boundaries

This repo does not hard-code a US-only financial advice limitation. It is configured for Israel-oriented deployments and supports Hebrew/English users with `ILS`, `USD`, and `EUR`.

The boundary is operational, not usefulness-limiting:

- Read and analysis tools can run directly.
- Simulation tools can run directly and may be powerful.
- Money-moving execution must go through VaultCoreLite v1.2 simulation confirmation.
- Raw execution tools such as `execute_transfer`, `buy_security`, `sell_security`, and `rebalance_portfolio` are blocked at the gateway.

Advanced finance tooling is controlled by deployment configuration:

```env
ENABLE_ADVANCED_FINANCE_TOOLS=true
```

This lets a properly authorized deployment enable the full wealth/tax/investment/retirement toolset while preserving the invariant that the banking core validates, executes and audits every mutation.
