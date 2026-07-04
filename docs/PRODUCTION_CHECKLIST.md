# Production checklist

v1.1 is production-oriented source, not a regulated banking certification.

Before handling real money:

- Run `dotnet build`, `dotnet test`, and PostgreSQL integration tests.
- Enable authentication/authorization and per-client rate limits.
- Use TLS everywhere.
- Verify plugin DLL signatures; use WDAC/App Control on Windows deployments.
- Use reviewed EF migration SQL, not blind runtime migrations.
- Enable backups, point-in-time restore, and restore drills.
- Monitor invariant audit, trial balance, schedule failures, and outbox lag.
- Add outbox relay to Kafka/RabbitMQ only after the DB core is stable.
- Add operational runbooks for failed schedule runs.
- Add reconciliation reports and regulator-facing audit exports.
