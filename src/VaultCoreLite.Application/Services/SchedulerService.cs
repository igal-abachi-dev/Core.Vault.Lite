using System.Data;
using Cronos;
using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Services;

public sealed record CreateScheduleRequest(Guid AccountId, string EventName, string Cron, string Timezone = "UTC");
public sealed record CreateDailyScheduleRequest(Guid AccountId, string EventName, int Hour, int Minute, string Timezone = "UTC");

public sealed class SchedulerService
{
    private readonly IScheduleRepository _schedules;
    private readonly PostingService _postings;
    private readonly IUnitOfWork _uow;

    public SchedulerService(IScheduleRepository schedules, PostingService postings, IUnitOfWork uow)
    {
        _schedules = schedules;
        _postings = postings;
        _uow = uow;
    }

    public Task<Schedule> CreateDailyScheduleAsync(CreateDailyScheduleRequest request, CancellationToken ct)
    {
        var cron = $"{request.Minute} {request.Hour} * * *";
        return CreateScheduleAsync(new CreateScheduleRequest(request.AccountId, request.EventName, cron, request.Timezone), ct);
    }

    public async Task<Schedule> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken ct)
    {
        if (request.AccountId == Guid.Empty) throw new ArgumentException("accountId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.EventName)) throw new ArgumentException("eventName is required.", nameof(request));
        var next = NextOccurrence(request.Cron, request.Timezone, DateTimeOffset.UtcNow);
        var schedule = new Schedule(EntityId.New(), request.AccountId, request.EventName.Trim(), request.Cron.Trim(), request.Timezone.Trim(), next);
        await _schedules.AddAsync(schedule, ct);
        await _uow.SaveChangesAsync(ct);
        return schedule;
    }

    public async Task<int> RunDueAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        var runnerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        IReadOnlyList<ClaimedScheduleRun> claimed = await _uow.ExecuteInTransactionAsync(async token =>
        {
            var rows = await _schedules.ClaimDueAsync(now, limit <= 0 ? 100 : limit, runnerId, token);
            await _uow.SaveChangesAsync(token);
            return rows;
        }, IsolationLevel.ReadCommitted, ct);

        var processed = 0;
        foreach (var item in claimed)
        {
            try
            {
                var runId = item.Run.Id.ToString("N");
                await _postings.RunScheduledEventAsync(item.Schedule.AccountId, item.Schedule.EventName, item.Run.DueAt, runId, ct);
                var next = NextOccurrence(item.Schedule.Cron, item.Schedule.Timezone, item.Run.DueAt.AddMilliseconds(1));
                await _schedules.MarkSucceededAndAdvanceAsync(item.Run.Id, item.Schedule.Id, next, ct);
                await _uow.SaveChangesAsync(ct);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _schedules.MarkFailedAsync(item.Run.Id, ex.Message, CancellationToken.None);
                await _uow.SaveChangesAsync(CancellationToken.None);
            }
        }
        return processed;
    }

    public static DateTimeOffset NextOccurrence(string cron, string timezone, DateTimeOffset after)
    {
        var expression = CronExpression.Parse(cron, cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone);
        var next = expression.GetNextOccurrence(after.UtcDateTime, tz);
        if (next is null) throw new InvalidOperationException($"Cron expression '{cron}' has no next occurrence.");
        return new DateTimeOffset(DateTime.SpecifyKind(next.Value, DateTimeKind.Utc));
    }
}
