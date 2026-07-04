using System.Data;
using System.Diagnostics;
using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Application.Mapping;
using VaultCoreLite.Contracts;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Services;

public sealed class PostingService
{
    private readonly IUnitOfWork _uow;
    private readonly ILedgerRepository _ledger;
    private readonly IAccountRepository _accounts;
    private readonly IOutboxRepository _outbox;
    private readonly IContractExecutionRepository _executions;
    private readonly IProductRuntime _runtime;

    public PostingService(IUnitOfWork uow, ILedgerRepository ledger, IAccountRepository accounts, IOutboxRepository outbox, IContractExecutionRepository executions, IProductRuntime runtime)
    {
        _uow = uow;
        _ledger = ledger;
        _accounts = accounts;
        _outbox = outbox;
        _executions = executions;
        _runtime = runtime;
    }

    public async Task<BatchResult> PostBatchAsync(BatchRequest request, CancellationToken ct)
    {
        IReadOnlyList<Posting> committedPostings = Array.Empty<Posting>();
        IReadOnlyList<Account> committedAccounts = Array.Empty<Account>();

        var result = await _uow.ExecuteInTransactionAsync(async token =>
        {
            request = await WithDefaultSettlementAccountAsync(request, token);
            var prior = await _ledger.FindBatchAsync(request.ClientId, request.ClientBatchId, token);
            if (prior is not null) return prior with { Replayed = true };

            var batchId = EntityId.New();
            var result = new BatchResult(batchId, request.ClientId, request.ClientBatchId, BatchStatus.Accepted, null, null, false);
            var existingClientTxs = await _ledger.LoadClientTransactionsAsync(request.ClientId, PostingExpander.ReferencedClientTransactionIds(request), token);

            ExpandedBatch expanded;
            try
            {
                expanded = PostingExpander.Expand(batchId, request, existingClientTxs);
            }
            catch (BusinessRejectionException rejection)
            {
                return await PersistRejectionAsync(result, request, rejection.Code, rejection.Message, token);
            }

            var accountIds = PostingExpander.AffectedAccountIds(expanded.Postings);
            var lockedAccounts = await _accounts.LockAccountsAsync(accountIds, token);
            if (lockedAccounts.Count != accountIds.Count) throw new KeyNotFoundException("One or more accounts were not found.");

            foreach (var account in lockedAccounts.Values)
            {
                if (account.Status != AccountStatus.Open)
                    return await PersistRejectionAsync(result, request, "ACCOUNT_NOT_OPEN", account.Id.ToString(), token);
                foreach (var posting in expanded.Postings.Where(x => x.AccountId == account.Id))
                {
                    if (!account.PermittedDenominations.Any(x => string.Equals(x, posting.Denomination, StringComparison.OrdinalIgnoreCase)))
                        return await PersistRejectionAsync(result, request, "DENOMINATION_NOT_PERMITTED", posting.Denomination, token);
                }
            }

            var balances = await _ledger.LoadBalancesAsync(accountIds, token);
            if (request.Source != BatchSource.Migration)
            {
                foreach (var account in lockedAccounts.Values.Where(x => !x.IsInternal && x.ProductVersion is not null))
                {
                    var postings = expanded.Postings.Where(x => x.AccountId == account.Id).ToArray();
                    var hookContext = ContractMapping.ToHookContext(account, request.ValueTimestamp ?? DateTimeOffset.UtcNow, balances.GetValueOrDefault(account.Id, Array.Empty<Balance>()), postings);
                    var contract = new ProductRuntimeContract(account.Id, account.ProductVersion!.ContractName, account.TSide, account.Parameters.ToDictionary(x => x.Name, x => x.Value));
                    var sw = Stopwatch.StartNew();
                    ProductHookResult hookResult;
                    try
                    {
                        hookResult = await _runtime.PrePostingAsync(contract, hookContext, token);
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        await _executions.RecordAsync(account.Id, "pre_posting", result.Id, contract.ContractName, "ERROR", new { error = ex.Message }, (int)sw.ElapsedMilliseconds, token);
                        return await PersistRejectionAsync(result, request, "CONTRACT_ERROR", ex.Message, token);
                    }
                    sw.Stop();
                    await _executions.RecordAsync(account.Id, "pre_posting", result.Id, contract.ContractName, hookResult.Rejection is null ? "ACCEPTED" : "REJECTED", new { hookResult.Rejection }, (int)sw.ElapsedMilliseconds, token);
                    if (hookResult.Rejection is not null)
                        return await PersistRejectionAsync(result, request, hookResult.Rejection.Code, hookResult.Rejection.Message, token);
                }
            }

            await _ledger.InsertAcceptedBatchAsync(result, request, expanded.Instructions, expanded.Postings, token);
            await _ledger.UpsertBalancesAsync(expanded.Postings, token);
            await _ledger.UpsertClientTransactionsAsync(expanded.UpdatedClientTransactions, token);
            await _outbox.AppendAsync("ledger.postings.v1", result.Id.ToString(), new { result.Id, result.ClientId, result.ClientBatchId, result.Status, PostingCount = expanded.Postings.Count }, token);
            await _uow.SaveChangesAsync(token);
            committedPostings = expanded.Postings;
            committedAccounts = lockedAccounts.Values.ToArray();
            return result;
        }, IsolationLevel.ReadCommitted, ct);

        if (result.Status == BatchStatus.Accepted && !result.Replayed && request.Source == BatchSource.Api)
            await RunPostPostingDirectivesAsync(request, result, committedPostings, committedAccounts, ct);

        return result;
    }

    public async Task RunScheduledEventAsync(Guid accountId, string eventName, DateTimeOffset dueAt, string runId, CancellationToken ct)
    {
        var account = await _accounts.GetAsync(accountId, ct) ?? throw new KeyNotFoundException("Account not found.");
        if (account.IsInternal || account.ProductVersion is null) return;
        var balances = await _ledger.ListBalancesAsync(account.Id, ct);
        var context = ContractMapping.ToHookContext(account, dueAt, balances, Array.Empty<Posting>());
        var contract = new ProductRuntimeContract(account.Id, account.ProductVersion.ContractName, account.TSide, account.Parameters.ToDictionary(x => x.Name, x => x.Value));
        var result = await _runtime.ScheduledEventAsync(contract, context, eventName, ct);
        await ApplyDirectivesAsync("scheduled_event", runId, dueAt, contract, result.Directives, ct);
    }

    private async Task RunPostPostingDirectivesAsync(BatchRequest request, BatchResult result, IReadOnlyList<Posting> postings, IEnumerable<Account> accounts, CancellationToken ct)
    {
        foreach (var account in accounts.Where(x => !x.IsInternal && x.ProductVersion is not null))
        {
            var balances = await _ledger.ListBalancesAsync(account.Id, ct);
            var ownPostings = postings.Where(x => x.AccountId == account.Id).ToArray();
            var context = ContractMapping.ToHookContext(account, request.ValueTimestamp ?? DateTimeOffset.UtcNow, balances, ownPostings);
            var contract = new ProductRuntimeContract(account.Id, account.ProductVersion!.ContractName, account.TSide, account.Parameters.ToDictionary(x => x.Name, x => x.Value));
            var hookResult = await _runtime.PostPostingAsync(contract, context, ct);
            await ApplyDirectivesAsync("post_posting", result.Id.ToString(), request.ValueTimestamp ?? DateTimeOffset.UtcNow, contract, hookResult.Directives, ct);
        }
    }

    private async Task ApplyDirectivesAsync(string hook, string trigger, DateTimeOffset effectiveTime, ProductRuntimeContract contract, IReadOnlyList<ProductPostingDirective> directives, CancellationToken ct)
    {
        if (directives.Count == 0) return;
        var drafts = new List<PostingDraft>();
        foreach (var directive in directives)
        {
            var accountId = directive.AccountId;
            if (accountId is null || accountId == Guid.Empty)
            {
                var accountRef = directive.AccountRef;
                if (string.IsNullOrWhiteSpace(accountRef) || string.Equals(accountRef, "self", StringComparison.OrdinalIgnoreCase)) accountId = contract.AccountId;
                else if (Guid.TryParse(accountRef, out var parsed)) accountId = parsed;
                else accountId = await _accounts.EnsureInternalAccountAsync(accountRef, new[] { directive.Denomination.ToUpperInvariant() }, ct);
            }
            drafts.Add(new PostingDraft(accountId.Value, directive.AccountAddress, directive.Asset, directive.Denomination, directive.Amount, directive.Credit, ContractMapping.ToDomainPhase(directive.Phase)));
        }
        var source = hook.Equals("scheduled_event", StringComparison.OrdinalIgnoreCase) ? BatchSource.Scheduler : BatchSource.Contract;
        var batch = new BatchRequest("contract", $"{hook}:{trigger}", source, effectiveTime, new[] { new InstructionRequest(InstructionType.Custom, null, null, null, null, null, null, false, drafts) });
        await PostBatchAsync(batch, ct);
    }

    private async Task<BatchRequest> WithDefaultSettlementAccountAsync(BatchRequest request, CancellationToken ct)
    {
        var updated = new List<InstructionRequest>();
        foreach (var instruction in request.Instructions)
        {
            if (NeedsSettlementAccount(instruction) && (!instruction.SettlementAccountId.HasValue || instruction.SettlementAccountId == Guid.Empty))
            {
                var denom = instruction.Denomination ?? "ILS";
                var settlement = await _accounts.EnsureInternalAccountAsync(LedgerConstants.SettlementSuspense, new[] { denom.ToUpperInvariant() }, ct);
                updated.Add(instruction with { SettlementAccountId = settlement });
            }
            else updated.Add(instruction);
        }
        return request with { Instructions = updated };
    }

    private static bool NeedsSettlementAccount(InstructionRequest instruction) => instruction.Type is InstructionType.InboundAuth or InstructionType.OutboundAuth or InstructionType.InboundHardSettlement or InstructionType.OutboundHardSettlement;

    private async Task<BatchResult> PersistRejectionAsync(BatchResult result, BatchRequest request, string code, string reason, CancellationToken ct)
    {
        var rejected = result with { Status = BatchStatus.Rejected, RejectionCode = code, RejectionReason = reason };
        await _ledger.InsertRejectedBatchAsync(rejected, request.Source, request.ValueTimestamp ?? DateTimeOffset.UtcNow, ct);
        await _outbox.AppendAsync("ledger.batches.v1", rejected.Id.ToString(), rejected, ct);
        await _uow.SaveChangesAsync(ct);
        return rejected;
    }
}
