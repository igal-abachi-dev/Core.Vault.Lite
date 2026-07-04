# Verify

```bash
npm install
npm run typecheck
npm test
npm run build
```

Run services:

```bash
cp .env.example .env
npm run dev:gateway
npm run dev:agent
```

Health checks:

```bash
curl http://localhost:4020/healthz
curl http://localhost:4010/healthz
```

Swagger/OpenAPI:

```text
http://localhost:4020/documentation
http://localhost:4010/documentation
```

Mock gateway test:

```bash
curl -X POST http://localhost:4020/v1/tools/simulate_investment_plan \
  -H 'Authorization: Bearer dev-gateway-token' \
  -H 'Content-Type: application/json' \
  -d '{
    "auth":{"userId":"u1","customerId":"c1","country":"IL","language":"he","allowedAccountIds":["11111111-1111-1111-1111-111111111111"]},
    "sourceAccountId":"11111111-1111-1111-1111-111111111111",
    "oneTimeContribution":"12000.00",
    "currency":"ILS",
    "years":10,
    "strategy":"balanced"
  }'
```
