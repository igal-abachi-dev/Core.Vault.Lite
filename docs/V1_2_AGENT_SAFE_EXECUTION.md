# VaultCoreLite C# v1.2 — Agent-Safe Money Execution

v1.2 makes every AI-driven money mutation follow this pattern:

```text
read -> simulate -> present preview -> explicit confirmation -> execute -> audit
```

## New endpoints

```http
POST /v1/simulations/transaction
GET  /v1/simulations/{id}
POST /v1/simulations/{id}:confirm
```

## Why this matters

An LLM/agent may reason and plan, but the core must remain deterministic. The agent can ask for a simulation without mutating money. Execution requires the returned `simulationId` and one-time `confirmationToken`.

## Guarantees

- The stored simulation contains the canonical request JSON and request hash.
- Confirmation validates the token with a constant-time hash check.
- Confirmation expires by default after 10 minutes.
- Execution reuses the stored request, not a new model-provided request.
- Execution uses the posting engine's normal idempotency key: `(clientId, clientBatchId)`.
- All simulation/confirmation/execution steps emit outbox events.
- Confirmation attempts are persisted in `simulation_confirmation_audits`.

## Agent rule

Do not expose `/v1/posting-instruction-batches` directly to the LLM. The later Agent Tool Gateway should expose execution tools only by calling `/v1/simulations/{id}:confirm` after a user-approved preview.
