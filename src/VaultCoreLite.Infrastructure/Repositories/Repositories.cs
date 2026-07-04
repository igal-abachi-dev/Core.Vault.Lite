using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Domain.Ledger;
using VaultCoreLite.Infrastructure.Persistence;

namespace VaultCoreLite.Infrastructure.Repositories;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly VaultDbContext _db;
    public EfUnitOfWork(VaultDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

public sealed class ProductRepository : BaseRepository<Product>, IProductRepository
{
    public ProductRepository(VaultDbContext context) : base(context) { }
    protected override IOrderedQueryable<Product> ApplyDefaultOrder(IQueryable<Product> query, bool descending = true) => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt);
    public Task<Product?> GetAsync(Guid id, CancellationToken ct) => Context.Products.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<int> NextVersionAsync(Guid productId, CancellationToken ct)
    {
        var max = await Context.ProductVersions.Where(x => x.ProductId == productId).MaxAsync(x => (int?)x.Version, ct);
        return (max ?? 0) + 1;
    }
    public ValueTask AddAsync(Product product, CancellationToken ct) => Context.Products.AddAsync(product, ct);
    public Task<ProductVersion?> GetVersionAsync(Guid id, CancellationToken ct) => Context.ProductVersions.FirstOrDefaultAsync(x => x.Id == id, ct);
    public ValueTask AddVersionAsync(ProductVersion version, CancellationToken ct) => Context.ProductVersions.AddAsync(version, ct);
    Task IProductRepository.AddAsync(Product product, CancellationToken ct) => AddAsync(product, ct).AsTask();
    Task IProductRepository.AddVersionAsync(ProductVersion version, CancellationToken ct) => AddVersionAsync(version, ct).AsTask();
}

public sealed class AccountRepository : BaseRepository<Account>, IAccountRepository
{
    public AccountRepository(VaultDbContext context) : base(context) { }
    protected override IOrderedQueryable<Account> ApplyDefaultOrder(IQueryable<Account> query, bool descending = true) => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt);

    public Task<Account?> GetAsync(Guid id, CancellationToken ct) => Context.Accounts.Include(x => x.ProductVersion).Include(x => x.Parameters).FirstOrDefaultAsync(x => x.Id == id, ct);
    public ValueTask AddAccountAsync(Account account, CancellationToken ct) => Context.Accounts.AddAsync(account, ct);
    Task IAccountRepository.AddAsync(Account account, CancellationToken ct) => AddAccountAsync(account, ct).AsTask();

    public async Task<IReadOnlyDictionary<Guid, Account>> LockAccountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, Account>();
        var ordered = ids.Distinct().OrderBy(x => x).ToArray();
        var locked = await Context.Accounts
            .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = ANY({ordered}) ORDER BY id FOR UPDATE")
            .Include(x => x.ProductVersion)
            .Include(x => x.Parameters)
            .ToListAsync(ct);
        return locked.ToDictionary(x => x.Id);
    }

    public async Task<Guid> EnsureInternalAccountAsync(string name, string[] denominations, CancellationToken ct)
    {
        var id = DeterministicGuid.FromName($"vaultlite-internal:{name.ToUpperInvariant()}");
        var existing = await Context.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is not null) return existing.Id;
        await Context.Accounts.AddAsync(new Account(id, true, null, denominations.Select(x => x.ToUpperInvariant()).ToArray(), TSide.Liability), ct);
        await Context.SaveChangesAsync(ct);
        return id;
    }

    public async Task SetParametersAsync(Guid accountId, IReadOnlyDictionary<string, string> parameters, string changedBy, CancellationToken ct)
    {
        foreach (var (name, value) in parameters)
        {
            var existing = await Context.AccountParameters.FirstOrDefaultAsync(x => x.AccountId == accountId && x.Name == name, ct);
            var oldValue = existing?.Value;
            if (existing is null) await Context.AccountParameters.AddAsync(new AccountParameter(accountId, name, value), ct);
            else existing.Update(value);
            await Context.AccountParameterHistory.AddAsync(new AccountParameterHistory(EntityId.New(), accountId, name, oldValue is null ? null : JsonSerializer.Serialize(oldValue), JsonSerializer.Serialize(value), changedBy), ct);
        }
    }
}

public sealed class LedgerRepository : ILedgerRepository
{
    private readonly VaultDbContext _db;
    public LedgerRepository(VaultDbContext db) => _db = db;

    public async Task<BatchResult?> FindBatchAsync(string clientId, string clientBatchId, CancellationToken ct)
    {
        var batch = await _db.PostingInstructionBatches.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId && x.ClientBatchId == clientBatchId, ct);
        return batch is null ? null : new BatchResult(batch.Id, batch.ClientId, batch.ClientBatchId, batch.Status, batch.RejectionCode, batch.RejectionReason, false);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Balance>>> LoadBalancesAsync(IReadOnlyList<Guid> accountIds, CancellationToken ct)
    {
        var balances = await _db.Balances.Where(x => accountIds.Contains(x.AccountId)).ToListAsync(ct);
        return balances.GroupBy(x => x.AccountId).ToDictionary(x => x.Key, x => (IReadOnlyList<Balance>)x.ToArray());
    }

    public async Task<IReadOnlyDictionary<string, ClientTransaction>> LoadClientTransactionsAsync(string clientId, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<string, ClientTransaction>();
        var txs = await _db.ClientTransactions.Where(x => x.ClientId == clientId && ids.Contains(x.ClientTransactionId)).ToListAsync(ct);
        return txs.ToDictionary(x => x.ClientTransactionId);
    }

    public async Task InsertRejectedBatchAsync(BatchResult result, BatchSource source, DateTimeOffset valueTimestamp, CancellationToken ct) =>
        await _db.PostingInstructionBatches.AddAsync(new PostingInstructionBatch(result.Id, result.ClientId, result.ClientBatchId, BatchStatus.Rejected, source, valueTimestamp, result.RejectionCode, result.RejectionReason), ct);

    public async Task InsertAcceptedBatchAsync(BatchResult result, BatchRequest request, IReadOnlyList<PostingInstruction> instructions, IReadOnlyList<Posting> postings, CancellationToken ct)
    {
        await _db.PostingInstructionBatches.AddAsync(new PostingInstructionBatch(result.Id, result.ClientId, result.ClientBatchId, BatchStatus.Accepted, request.Source, request.ValueTimestamp ?? DateTimeOffset.UtcNow), ct);
        await _db.PostingInstructions.AddRangeAsync(instructions, ct);
        await _db.Postings.AddRangeAsync(postings, ct);
    }

    public async Task UpsertBalancesAsync(IReadOnlyList<Posting> postings, CancellationToken ct)
    {
        foreach (var posting in postings)
        {
            var balance = await _db.Balances.FirstOrDefaultAsync(x => x.AccountId == posting.AccountId && x.AccountAddress == posting.AccountAddress && x.Asset == posting.Asset && x.Denomination == posting.Denomination && x.Phase == posting.Phase, ct);
            if (balance is null)
            {
                balance = new Balance(posting.AccountId, posting.AccountAddress, posting.Asset, posting.Denomination, posting.Phase);
                await _db.Balances.AddAsync(balance, ct);
            }
            balance.Apply(posting);
        }
    }

    public async Task UpsertClientTransactionsAsync(IReadOnlyList<ClientTransaction> clientTransactions, CancellationToken ct)
    {
        foreach (var tx in clientTransactions)
        {
            var existing = await _db.ClientTransactions.FirstOrDefaultAsync(x => x.ClientId == tx.ClientId && x.ClientTransactionId == tx.ClientTransactionId, ct);
            if (existing is null) await _db.ClientTransactions.AddAsync(tx, ct);
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(tx);
            }
        }
    }

    public async Task<IReadOnlyList<Balance>> ListBalancesAsync(Guid accountId, CancellationToken ct) => await _db.Balances.AsNoTracking().Where(x => x.AccountId == accountId).OrderBy(x => x.AccountAddress).ThenBy(x => x.Denomination).ThenBy(x => x.Phase).ToListAsync(ct);
    public async Task<IReadOnlyList<Posting>> ListPostingsAsync(Guid accountId, int limit, CancellationToken ct) => await _db.Postings.AsNoTracking().Where(x => x.AccountId == accountId).OrderByDescending(x => x.InsertedAt).Take(limit <= 0 || limit > 1000 ? 100 : limit).ToListAsync(ct);
    public Task<ClientTransaction?> GetClientTransactionAsync(string clientId, string clientTransactionId, CancellationToken ct) => _db.ClientTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.ClientId == clientId && x.ClientTransactionId == clientTransactionId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> TrialBalanceAsync(string? denomination, CancellationToken ct)
    {
        var rows = await _db.Postings.AsNoTracking().Where(x => string.IsNullOrEmpty(denomination) || x.Denomination == denomination).GroupBy(x => x.AccountId).Select(g => new { AccountId = g.Key, Net = g.Sum(x => x.Credit ? x.Amount : -x.Amount) }).ToListAsync(ct);
        return rows.ToDictionary(x => x.AccountId, x => x.Net.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<AuditReport> AuditInvariantsAsync(CancellationToken ct)
    {
        var postings = await _db.Postings.AsNoTracking().ToListAsync(ct);
        var balances = await _db.Balances.AsNoTracking().ToListAsync(ct);
        var mismatches = new List<string>();
        foreach (var group in postings.GroupBy(x => new { x.AccountId, x.AccountAddress, x.Asset, x.Denomination, x.Phase }))
        {
            var cached = balances.FirstOrDefault(x => x.AccountId == group.Key.AccountId && x.AccountAddress == group.Key.AccountAddress && x.Asset == group.Key.Asset && x.Denomination == group.Key.Denomination && x.Phase == group.Key.Phase);
            var credits = group.Where(x => x.Credit).Sum(x => x.Amount);
            var debits = group.Where(x => !x.Credit).Sum(x => x.Amount);
            if (cached is null || cached.TotalCredits != credits || cached.TotalDebits != debits) mismatches.Add($"{group.Key.AccountId}/{group.Key.AccountAddress}/{group.Key.Denomination}/{group.Key.Phase}");
        }
        var outOfBalance = postings.GroupBy(x => new { x.BatchId, x.Asset, x.Denomination }).Where(g => g.Where(x => x.Credit).Sum(x => x.Amount) != g.Where(x => !x.Credit).Sum(x => x.Amount)).Select(g => $"{g.Key.BatchId}/{g.Key.Asset}/{g.Key.Denomination}").ToArray();
        var trial = postings.GroupBy(x => x.Denomination).ToDictionary(g => g.Key, g => g.Sum(x => x.Credit ? x.Amount : -x.Amount).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new AuditReport(mismatches.Count == 0 && outOfBalance.Length == 0 && trial.Values.All(x => x == "0" || x == "0.000000000"), mismatches, outOfBalance, trial, DateTimeOffset.UtcNow);
    }
}


public sealed class SimulationRepository : ISimulationRepository
{
    private readonly VaultDbContext _db;
    public SimulationRepository(VaultDbContext db) => _db = db;
    public async Task AddAsync(MoneySimulation simulation, CancellationToken ct) => await _db.MoneySimulations.AddAsync(simulation, ct);
    public Task<MoneySimulation?> GetAsync(Guid id, bool tracking, CancellationToken ct)
    {
        var query = tracking ? _db.MoneySimulations : _db.MoneySimulations.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }
    public async Task AddConfirmationAuditAsync(SimulationConfirmationAudit audit, CancellationToken ct) => await _db.SimulationConfirmationAudits.AddAsync(audit, ct);
}

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly VaultDbContext _db;
    public OutboxRepository(VaultDbContext db) => _db = db;
    public async Task AppendAsync(string topic, string key, object payload, CancellationToken ct) => await _db.OutboxEvents.AddAsync(new OutboxEvent(topic, key, JsonSerializer.Serialize(payload)), ct);
    public async Task<IReadOnlyList<OutboxRow>> ReadAsync(long afterSeq, int limit, CancellationToken ct) => await _db.OutboxEvents.AsNoTracking().Where(x => x.Seq > afterSeq).OrderBy(x => x.Seq).Take(limit <= 0 || limit > 500 ? 100 : limit).Select(x => new OutboxRow(x.Seq, x.Topic, x.Key, x.PayloadJson, x.InsertedAt)).ToListAsync(ct);
}

public sealed class ContractExecutionRepository : IContractExecutionRepository
{
    private readonly VaultDbContext _db;
    public ContractExecutionRepository(VaultDbContext db) => _db = db;
    public async Task RecordAsync(Guid? accountId, string hook, Guid triggerId, string contractName, string outcome, object detail, int durationMs, CancellationToken ct) => await _db.ContractExecutions.AddAsync(new ContractExecution(EntityId.New(), accountId, hook, triggerId, contractName, outcome, JsonSerializer.Serialize(detail), durationMs), ct);
}

public sealed class ScheduleRepository : IScheduleRepository
{
    private readonly VaultDbContext _db;
    public ScheduleRepository(VaultDbContext db) => _db = db;
    public async Task AddAsync(Schedule schedule, CancellationToken ct) => await _db.Schedules.AddAsync(schedule, ct);

    public async Task<IReadOnlyList<ClaimedScheduleRun>> ClaimDueAsync(DateTimeOffset now, int limit, string runnerId, CancellationToken ct)
    {
        limit = limit <= 0 || limit > 500 ? 100 : limit;
        var due = await _db.Schedules
            .FromSqlInterpolated($"""
SELECT * FROM schedules
WHERE status = 'ACTIVE' AND next_due_at <= {now}
ORDER BY next_due_at
LIMIT {limit}
FOR UPDATE SKIP LOCKED
""")
            .ToListAsync(ct);

        var claimed = new List<ClaimedScheduleRun>(due.Count);
        foreach (var schedule in due)
        {
            var existing = await _db.ScheduleRuns.AsNoTracking()
                .AnyAsync(x => x.ScheduleId == schedule.Id && x.DueAt == schedule.NextDueAt, ct);
            if (existing) continue;
            var run = new ScheduleRun(EntityId.New(), schedule.Id, schedule.NextDueAt, runnerId);
            await _db.ScheduleRuns.AddAsync(run, ct);
            claimed.Add(new ClaimedScheduleRun(schedule, run));
        }
        return claimed;
    }

    public async Task MarkSucceededAndAdvanceAsync(Guid runId, Guid scheduleId, DateTimeOffset nextDueAt, CancellationToken ct)
    {
        var run = await _db.ScheduleRuns.FirstAsync(x => x.Id == runId, ct);
        run.Succeed();
        var schedule = await _db.Schedules.FirstAsync(x => x.Id == scheduleId, ct);
        schedule.Advance(nextDueAt);
    }

    public async Task MarkFailedAsync(Guid runId, string error, CancellationToken ct)
    {
        var run = await _db.ScheduleRuns.FirstAsync(x => x.Id == runId, ct);
        run.Fail(error);
    }
}

internal static class DeterministicGuid
{
    public static Guid FromName(string name)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
        var guidBytes = bytes[..16];
        return new Guid(guidBytes);
    }
}
