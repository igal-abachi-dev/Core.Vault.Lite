# Architecture

```text
apps/agent-service
  Fastify HTTP API
  Vercel AI SDK generateText + tools
  Swarm/handoff runner
  Does not know VaultCoreLite credentials

apps/tool-gateway
  Fastify HTTP API
  Tool policy
  Typed VaultCoreLite client
  Direct execution blocked
  Simulation confirmation execution allowed

packages/schemas
  Zod schemas shared by both services

packages/agent-policy
  Risk classification, account/currency/country policy, error redaction

packages/banking-tools
  AI SDK tool definitions that call the gateway
```

The gateway is intentionally separate so it can later be deployed closer to VaultCoreLite, behind stricter auth, or converted into a BFF.
