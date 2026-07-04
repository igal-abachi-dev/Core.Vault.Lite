using System.Data;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Abstractions;

public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IProductRepository
{
    Task<Product?> GetAsync(Guid id, CancellationToken ct);
    Task<int> NextVersionAsync(Guid productId, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    Task<ProductVersion?> GetVersionAsync(Guid id, CancellationToken ct);
    Task AddVersionAsync(ProductVersion version, CancellationToken ct);
}

public interface IAccountRepository
{
    Task<Account?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, Account>> LockAccountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct);
    Task<Guid> EnsureInternalAccountAsync(string name, string[] denominations, CancellationToken ct);
    Task SetParametersAsync(Guid accountId, IReadOnlyDictionary<string, string> parameters, string changedBy, CancellationToken ct);
}

public interface ILedgerRepository
{
    Task<BatchResult?> FindBatchAsync(string clientId, string clientBatchId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Balance>>> LoadBalancesAsync(IReadOnlyList<Guid> accountIds, CancellationToken ct);
    Task<IReadOnlyDictionary<string, ClientTransaction>> LoadClientTransactionsAsync(string clientId, IReadOnlyList<string> clientTransactionIds, CancellationToken ct);
    Task InsertRejectedBatchAsync(BatchResult result, BatchSource source, DateTimeOffset valueTimestamp, CancellationToken ct);
    Task InsertAcceptedBatchAsync(BatchResult result, BatchRequest request, IReadOnlyList<PostingInstruction> instructions, IReadOnlyList<Posting> postings, CancellationToken ct);
    Task UpsertBalancesAsync(IReadOnlyList<Posting> postings, CancellationToken ct);
    Task UpsertClientTransactionsAsync(IReadOnlyList<ClientTransaction> clientTransactions, CancellationToken ct);
    Task<IReadOnlyList<Balance>> ListBalancesAsync(Guid accountId, CancellationToken ct);
    Task<IReadOnlyList<Posting>> ListPostingsAsync(Guid accountId, int limit, CancellationToken ct);
    Task<ClientTransaction?> GetClientTransactionAsync(string clientId, string clientTransactionId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> TrialBalanceAsync(string? denomination, CancellationToken ct);
    Task<AuditReport> AuditInvariantsAsync(CancellationToken ct);
}


public interface ISimulationRepository
{
    Task AddAsync(MoneySimulation simulation, CancellationToken ct);
    Task<MoneySimulation?> GetAsync(Guid id, bool tracking, CancellationToken ct);
    Task AddConfirmationAuditAsync(SimulationConfirmationAudit audit, CancellationToken ct);
}

public interface IOutboxRepository
{
    Task AppendAsync(string topic, string key, object payload, CancellationToken ct);
    Task<IReadOnlyList<OutboxRow>> ReadAsync(long afterSeq, int limit, CancellationToken ct);
}

public sealed record OutboxRow(long Seq, string Topic, string Key, string PayloadJson, DateTimeOffset InsertedAt);

public interface IContractExecutionRepository
{
    Task RecordAsync(Guid? accountId, string hook, Guid triggerId, string contractName, string outcome, object detail, int durationMs, CancellationToken ct);
}

public sealed record ClaimedScheduleRun(Schedule Schedule, ScheduleRun Run);

public interface IScheduleRepository
{
    Task AddAsync(Schedule schedule, CancellationToken ct);
    Task<IReadOnlyList<ClaimedScheduleRun>> ClaimDueAsync(DateTimeOffset now, int limit, string runnerId, CancellationToken ct);
    Task MarkSucceededAndAdvanceAsync(Guid runId, Guid scheduleId, DateTimeOffset nextDueAt, CancellationToken ct);
    Task MarkFailedAsync(Guid runId, string error, CancellationToken ct);
}
