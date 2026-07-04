# v1.2 changes

v1.2 makes VaultCoreLite safer for AI agent tool use.

## Added

- `MoneySimulation` domain entity.
- `SimulationConfirmationAudit` domain entity.
- `SimulationService` application service.
- `ISimulationRepository` and EF repository implementation.
- EF Core model mappings for simulation tables.
- Migration `20260704180000_AgentSimulations`.
- API endpoints:
  - `POST /v1/simulations/transaction`
  - `GET /v1/simulations/{id}`
  - `POST /v1/simulations/{id}:confirm`
- Outbox topics:
  - `money.simulations.v1`
  - `money.simulation_confirmations.v1`
  - `money.simulation_executions.v1`

## Agent-safe execution rule

The LLM/agent must not call the raw posting endpoint directly for user money movement.

Correct flow:

```text
simulate -> show preview -> user confirms -> confirm endpoint executes stored request
```

## Confirmation behavior

- Confirmation token is generated once and returned only from simulation creation.
- Stored token is SHA-256 hashed with the simulation id.
- Token comparison uses constant-time comparison.
- Confirmation expires by default after 10 minutes.
- Confirmation executes the stored request JSON, not a new request supplied by the agent.
- Posting execution remains idempotent through `(clientId, clientBatchId)`.
