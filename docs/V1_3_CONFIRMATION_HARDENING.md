# v1.3 Confirmation Hardening

This patch addresses the senior review feedback around human-in-the-loop confirmation.

## Fixed

### 1. Confirmation token no longer transits the LLM

Before: `simulate_*` returned the core confirmation token into the agent tool result, so the model could see the token and potentially call `confirm_simulation` itself.

Now:

- the Tool Gateway captures `confirmationToken` from VaultCoreLite
- the token is stored out-of-band in `ConfirmationTokenStore`
- the model-visible tool result is redacted
- the response contains only `humanConfirmation` metadata:
  - `simulationId`
  - `path`
  - display instructions
- the Vercel/AI SDK tool set no longer exposes `confirm_simulation`
- the UI must call `POST /v1/human-confirmations/{simulationId}/confirm`

This turns human approval from a prompt convention into a structural boundary.

> Production note: the included `InMemoryConfirmationTokenStore` is for a single gateway process. In production, replace it with Redis/Postgres/encrypted session storage so confirmations survive restarts and work across multiple gateway replicas.

### 2. Confirmed-but-not-Executed limbo recovery

Before: `ConfirmAndExecuteAsync` committed `Confirmed`, then posted the batch, then marked `Executed`. A process crash between those phases could leave the simulation stuck at `Confirmed`.

Now:

- if a retry sees `Confirmed`, it reconciles:
  - if the batch already exists by `(clientId, clientBatchId)`, mark the simulation `Executed`
  - if the batch does not exist, safely re-run the stored `BatchRequest`
- the posting engine idempotency key remains the money-path backstop

### 3. Row lock on confirmation

Before: confirming used a normal tracked EF query under `ReadCommitted`.

Now:

- confirm path uses `SELECT * FROM money_simulations WHERE id = ... FOR UPDATE`
- two concurrent confirms become single-winner at the confirmation gate
- idempotent posting still protects the money path

### 4. Auth headers forwarded by banking tool client

The agent wrapper now forwards user/customer/account scope headers to the Tool Gateway so policy decisions use the real request context instead of default demo context.

## Safe execution flow

```text
Agent calls simulate_*
  ↓
Tool Gateway captures token and redacts it from model-visible output
  ↓
Agent explains preview
  ↓
UI renders confirmation button from pendingConfirmations[]
  ↓
UI calls /v1/human-confirmations/{simulationId}/confirm
  ↓
Tool Gateway injects stored token server-side
  ↓
VaultCoreLite executes the stored BatchRequest
  ↓
Ledger + audit + outbox
```
