# v1.1 changes

- Replaced the simple hand-rolled daily scheduler with Cronos-based cron parsing.
- Added `SchedulerBackgroundService` for automatic due schedule processing.
- Added `schedule_runs` with unique `(schedule_id, due_at)` for exactly-once due-time claiming.
- Added API rate limiting using ASP.NET Core's built-in rate limiter.
- Added `POST /v1/schedules` for arbitrary cron expressions.
- Kept `POST /v1/schedules/daily` as a convenience endpoint.
- Added `BankProducts.Plugin` product pack with current account, savings, term deposit, wallet, personal loan, mortgage loan, and credit card.
- Switched default plugin config from `SavingsProduct.Plugin.dll` to `BankProducts.Plugin.dll`.
- Documented product contract names and schedule events.
