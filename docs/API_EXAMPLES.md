# API examples

All examples assume `http://localhost:8080`.

## Product

Create a product:

```bash
curl -X POST http://localhost:8080/v1/products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Savings"}'
```

Create a savings product version:

```bash
curl -X POST http://localhost:8080/v1/product-versions \
  -H 'Content-Type: application/json' \
  -d '{"productId":"PRODUCT_ID","tSide":"Liability","denominations":["ILS"],"contractName":"SavingsAccount","contractVersion":"1.1.0"}'
```

Other useful `contractName` values: `CurrentAccount`, `TermDeposit`, `Wallet`, `PersonalLoan`, `MortgageLoan`, `CreditCard`.

Activate it:

```bash
curl -X POST http://localhost:8080/v1/product-versions/VERSION_ID:activate
```

## Account

```bash
curl -X POST http://localhost:8080/v1/accounts \
  -H 'Content-Type: application/json' \
  -d '{"isInternal":false,"productVersionId":"VERSION_ID","permittedDenominations":["ILS"],"parameters":{"min_balance":"0","annual_rate":"0.0365"}}'
```

## Deposit

```bash
curl -X POST http://localhost:8080/v1/posting-instruction-batches \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"demo","clientBatchId":"dep-1","source":"Api","instructions":[{"type":"InboundHardSettlement","accountId":"ACCOUNT_ID","amount":1000,"denomination":"ILS"}]}'
```

## Outbound auth / settlement / release

```bash
curl -X POST http://localhost:8080/v1/posting-instruction-batches \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"demo","clientBatchId":"auth-1","source":"Api","instructions":[{"type":"OutboundAuth","accountId":"ACCOUNT_ID","clientTransactionId":"ctx-1","amount":100,"denomination":"ILS"}]}'

curl -X POST http://localhost:8080/v1/posting-instruction-batches \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"demo","clientBatchId":"settle-1","source":"Api","instructions":[{"type":"Settlement","accountId":"ACCOUNT_ID","clientTransactionId":"ctx-1","amount":60,"denomination":"ILS"}]}'

curl -X POST http://localhost:8080/v1/posting-instruction-batches \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"demo","clientBatchId":"release-1","source":"Api","instructions":[{"type":"Release","accountId":"ACCOUNT_ID","clientTransactionId":"ctx-1"}]}'
```

## Schedule savings interest accrual

```bash
curl -X POST http://localhost:8080/v1/schedules \
  -H 'Content-Type: application/json' \
  -d '{"accountId":"ACCOUNT_ID","eventName":"ACCRUE_INTEREST","cron":"0 0 * * *","timezone":"UTC"}'
```

Manual due-run trigger:

```bash
curl -X POST http://localhost:8080/v1/scheduler/run-due
```

## Reads

```bash
curl http://localhost:8080/v1/accounts/ACCOUNT_ID/balances
curl http://localhost:8080/v1/accounts/ACCOUNT_ID/postings
curl http://localhost:8080/v1/client-transactions/demo/ctx-1
curl http://localhost:8080/v1/events
curl http://localhost:8080/v1/audit/invariants
```


## v1.2 simulation-first execution

Create a simulation:

```bash
curl -X POST http://localhost:8080/v1/simulations/transaction \
  -H 'Content-Type: application/json' \
  -d '{
    "requestedBy":"user-123",
    "batch":{
      "clientId":"agent-user-123",
      "clientBatchId":"",
      "source":"Api",
      "valueTimestamp":"2026-07-04T00:00:00Z",
      "instructions":[{
        "type":"Transfer",
        "accountId":"SOURCE_ACCOUNT_UUID",
        "targetAccountId":"TARGET_ACCOUNT_UUID",
        "amount":100.00,
        "denomination":"ILS",
        "final":false
      }]
    }
  }'
```

Confirm and execute only after the user approves the preview:

```bash
curl -X POST http://localhost:8080/v1/simulations/SIMULATION_UUID:confirm \
  -H 'Content-Type: application/json' \
  -d '{"confirmedBy":"user-123","confirmationToken":"TOKEN_FROM_SIMULATION"}'
```
