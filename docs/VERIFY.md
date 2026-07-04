# Verify v1.1 locally

The generation sandbox used to create this package does not include the .NET SDK, so run these locally.

## Restore/build/test

```bash
dotnet restore
dotnet build
dotnet test
```

## Database

```bash
docker compose up -d postgres
dotnet ef database update --project src/VaultCoreLite.Infrastructure --startup-project src/VaultCoreLite.Api
```

## Run API

```bash
dotnet run --project src/VaultCoreLite.Api
```

## Plugin mode smoke test

```bash
dotnet build plugins/BankProducts.Plugin/BankProducts.Plugin.csproj -c Release
mkdir -p src/VaultCoreLite.Api/bin/Debug/net10.0/plugins
cp plugins/BankProducts.Plugin/bin/Release/net10.0/BankProducts.Plugin.dll src/VaultCoreLite.Api/bin/Debug/net10.0/plugins/
ASPNETCORE_ENVIRONMENT=Plugin dotnet run --project src/VaultCoreLite.Api
```

## Minimum smoke flow

1. Create product.
2. Create product version with `contractName=CurrentAccount` or `SavingsAccount`.
3. Activate version.
4. Create account.
5. Post inbound hard settlement.
6. Post outbound auth.
7. Partially settle.
8. Release remaining.
9. Read balances.
10. Read client transaction.
11. Read outbox events.
12. Run invariant audit.
13. Create schedule and let `SchedulerBackgroundService` process it, or call `/v1/scheduler/run-due`.

## Recommended next verification before real production

- Add Testcontainers PostgreSQL integration tests around the above flow.
- Run concurrent posting tests against the same accounts.
- Test idempotency replay for both accepted and rejected batches.
- Test plugin-mode loading with Authenticode enabled on Windows.
- Test scheduler across two API instances to confirm only one `schedule_run` is created per due time.
