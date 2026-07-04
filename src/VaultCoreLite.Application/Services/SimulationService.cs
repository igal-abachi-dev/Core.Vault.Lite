using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Application.Mapping;
using VaultCoreLite.Contracts;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Services;

public sealed class SimulationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IUnitOfWork _uow;
    private readonly ILedgerRepository _ledger;
    private readonly IAccountRepository _accounts;
    private readonly IOutboxRepository _outbox;
    private readonly IContractExecutionRepository _executions;
    private readonly ISimulationRepository _simulations;
    private readonly IProductRuntime _runtime;
    private readonly PostingService _postingService;

    public SimulationService(IUnitOfWork uow, ILedgerRepository ledger, IAccountRepository accounts, IOutboxRepository outbox, IContractExecutionRepository executions, ISimulationRepository simulations, IProductRuntime runtime, PostingService postingService)
    {
        _uow = uow;
        _ledger = ledger;
        _accounts = accounts;
        _outbox = outbox;
        _executions = executions;
        _simulations = simulations;
        _runtime = runtime;
        _postingService = postingService;
    }

    public async Task<SimulationResult> SimulateTransactionAsync(SimulateTransactionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RequestedBy)) throw new ArgumentException("requestedBy is required.");
        if (string.IsNullOrWhiteSpace(request.Batch.ClientId)) throw new ArgumentException("batch.clientId is required.");

        var simulationId = EntityId.New();
        var batch = request.Batch;
        if (string.IsNullOrWhiteSpace(batch.ClientBatchId))
            batch = batch with { ClientBatchId = $"sim:{simulationId:N}" };
        if (batch.ValueTimestamp is null)
            batch = batch with { ValueTimestamp = DateTimeOffset.UtcNow };
        if (batch.Source == default)
            batch = batch with { Source = BatchSource.Api };

        var token = NewToken();
        var tokenHash = HashToken(simulationId, token);
        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(request.ExpiresInMinutes ?? 10, 1, 60));

        var preview = await PreviewAsync(batch, ct);
        var requestJson = JsonSerializer.Serialize(batch, JsonOptions);
        var previewJson = JsonSerializer.Serialize(preview, JsonOptions);
        var requestHash = HashJson(requestJson);

        var simulation = new MoneySimulation(simulationId, SimulationKind.Transaction, batch.ClientId, batch.ClientBatchId, request.RequestedBy, requestHash, requestJson, previewJson, tokenHash, expires);
        if (!preview.Accepted)
            simulation.MarkRejected(preview.RejectionCode ?? "SIMULATION_REJECTED", preview.RejectionReason ?? "Simulation was rejected.");

        await _simulations.AddAsync(simulation, ct);
        await _outbox.AppendAsync("money.simulations.v1", simulation.Id.ToString(), new { simulation.Id, simulation.Kind, simulation.ClientId, simulation.ClientBatchId, simulation.Status, preview.Accepted, preview.RejectionCode }, ct);
        await _uow.SaveChangesAsync(ct);

        return new SimulationResult(simulation.Id, simulation.Kind, simulation.Status, simulation.ClientId, simulation.ClientBatchId, simulation.RequestedBy, simulation.ExpiresAt, preview.Accepted ? token : string.Empty, preview);
    }

    public async Task<SimulationDetails?> GetAsync(Guid simulationId, CancellationToken ct)
    {
        var simulation = await _simulations.GetAsync(simulationId, tracking: false, ct);
        if (simulation is null) return null;
        return ToDetails(simulation);
    }

    public async Task<ConfirmedSimulationResult> ConfirmAndExecuteAsync(Guid simulationId, ConfirmSimulationRequest request, CancellationToken ct)
    {
        var prepared = await _uow.ExecuteInTransactionAsync(async token =>
        {
            var simulation = await _simulations.GetAsync(simulationId, tracking: true, token) ?? throw new KeyNotFoundException("Simulation not found.");
            var actor = string.IsNullOrWhiteSpace(request.ConfirmedBy) ? "unknown" : request.ConfirmedBy.Trim();

            if (simulation.Status == SimulationStatus.Executed && simulation.ExecutedBatchId is Guid alreadyExecuted)
            {
                await _simulations.AddConfirmationAuditAsync(new SimulationConfirmationAudit(EntityId.New(), simulation.Id, ConfirmationStatus.Replay, actor, "Simulation already executed."), token);
                await _uow.SaveChangesAsync(token);
                var existing = await _ledger.FindBatchAsync(simulation.ClientId, simulation.ClientBatchId, token) ?? new BatchResult(alreadyExecuted, simulation.ClientId, simulation.ClientBatchId, BatchStatus.Accepted, null, null, true);
                return ConfirmPrepared.Replayed(simulation.Id, simulation.Status, existing with { Replayed = true });
            }

            if (simulation.Status != SimulationStatus.PendingConfirmation)
            {
                await _simulations.AddConfirmationAuditAsync(new SimulationConfirmationAudit(EntityId.New(), simulation.Id, ConfirmationStatus.Failed, actor, $"Invalid status {simulation.Status}."), token);
                await _outbox.AppendAsync("money.simulation_confirmations.v1", simulation.Id.ToString(), new { simulation.Id, Status = "FAILED", Reason = "invalid_status", simulation.Status }, token);
                await _uow.SaveChangesAsync(token);
                return ConfirmPrepared.Failed(simulation.Id, simulation.Status, $"Simulation is {simulation.Status} and cannot be confirmed.");
            }

            if (simulation.IsExpired(DateTimeOffset.UtcNow))
            {
                simulation.MarkExpired();
                await _simulations.AddConfirmationAuditAsync(new SimulationConfirmationAudit(EntityId.New(), simulation.Id, ConfirmationStatus.Expired, actor, "Confirmation window expired."), token);
                await _outbox.AppendAsync("money.simulation_confirmations.v1", simulation.Id.ToString(), new { simulation.Id, simulation.Status, Reason = "expired" }, token);
                await _uow.SaveChangesAsync(token);
                return ConfirmPrepared.Failed(simulation.Id, simulation.Status, "Simulation confirmation window expired.");
            }

            if (!ConstantTimeEquals(simulation.ConfirmationTokenHash, HashToken(simulation.Id, request.ConfirmationToken)))
            {
                await _simulations.AddConfirmationAuditAsync(new SimulationConfirmationAudit(EntityId.New(), simulation.Id, ConfirmationStatus.Failed, actor, "Invalid confirmation token."), token);
                await _outbox.AppendAsync("money.simulation_confirmations.v1", simulation.Id.ToString(), new { simulation.Id, Status = "FAILED", Reason = "invalid_token" }, token);
                await _uow.SaveChangesAsync(token);
                return ConfirmPrepared.UnauthorizedFailure(simulation.Id, simulation.Status, "Invalid confirmation token.");
            }

            var batch = JsonSerializer.Deserialize<BatchRequest>(simulation.RequestJson, JsonOptions) ?? throw new InvalidOperationException("Stored simulation request is invalid.");
            var currentHash = HashJson(JsonSerializer.Serialize(batch, JsonOptions));
            if (!ConstantTimeEquals(simulation.RequestHash, currentHash))
            {
                await _simulations.AddConfirmationAuditAsync(new SimulationConfirmationAudit(EntityId.New(), simulation.Id, ConfirmationStatus.Failed, actor, "Request hash mismatch."), token);
                await _outbox.AppendAsync("money.simulation_confirmations.v1", simulation.Id.ToString(), new { simulation.Id, Status = "FAILED", Reason = "request_hash_mismatch" }, token);
                await _uow.SaveChangesAsync(token);
                return ConfirmPrepared.Failed(simulation.Id, simulation.Status, "Simulation request integrity check failed.");
            }

            simulation.Confirm();
            await _simulations.AddConfirmationAuditAsync(new SimulationConfirmationAudit(EntityId.New(), simulation.Id, ConfirmationStatus.Accepted, actor, "Confirmed by user."), token);
            await _outbox.AppendAsync("money.simulation_confirmations.v1", simulation.Id.ToString(), new { simulation.Id, simulation.Status, ConfirmedBy = actor }, token);
            await _uow.SaveChangesAsync(token);
            return ConfirmPrepared.Ready(simulation.Id, simulation.Status, batch);
        }, IsolationLevel.ReadCommitted, ct);

        if (prepared.ErrorMessage is not null)
        {
            if (prepared.Unauthorized) throw new UnauthorizedAccessException(prepared.ErrorMessage);
            throw new InvalidOperationException(prepared.ErrorMessage);
        }

        if (prepared.ExistingResult is not null)
            return new ConfirmedSimulationResult(prepared.SimulationId, prepared.Status, prepared.ExistingResult);

        var result = await _postingService.PostBatchAsync(prepared.Batch!, ct);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            var simulation = await _simulations.GetAsync(simulationId, tracking: true, token) ?? throw new KeyNotFoundException("Simulation not found.");
            simulation.MarkExecuted(result.Id);
            await _outbox.AppendAsync("money.simulation_executions.v1", simulation.Id.ToString(), new { simulation.Id, BatchId = result.Id, result.Status, result.Replayed }, token);
            await _uow.SaveChangesAsync(token);
            return true;
        }, IsolationLevel.ReadCommitted, ct);

        return new ConfirmedSimulationResult(prepared.SimulationId, SimulationStatus.Executed, result);
    }

    private sealed record ConfirmPrepared(Guid SimulationId, SimulationStatus Status, BatchRequest? Batch, BatchResult? ExistingResult, string? ErrorMessage, bool Unauthorized)
    {
        public static ConfirmPrepared Ready(Guid simulationId, SimulationStatus status, BatchRequest batch) => new(simulationId, status, batch, null, null, false);
        public static ConfirmPrepared Replayed(Guid simulationId, SimulationStatus status, BatchResult result) => new(simulationId, status, null, result, null, false);
        public static ConfirmPrepared Failed(Guid simulationId, SimulationStatus status, string message) => new(simulationId, status, null, null, message, false);
        public static ConfirmPrepared UnauthorizedFailure(Guid simulationId, SimulationStatus status, string message) => new(simulationId, status, null, null, message, true);
    }

    private async Task<SimulationPreview> PreviewAsync(BatchRequest batch, CancellationToken ct)
    {
        batch = await WithDefaultSettlementAccountAsync(batch, ct);
        var existingClientTxs = await _ledger.LoadClientTransactionsAsync(batch.ClientId, PostingExpander.ReferencedClientTransactionIds(batch), ct);
        ExpandedBatch expanded;
        try
        {
            expanded = PostingExpander.Expand(EntityId.New(), batch, existingClientTxs);
        }
        catch (BusinessRejectionException rejection)
        {
            return new SimulationPreview(false, rejection.Code, rejection.Message, Array.Empty<SimulationPostingPreview>(), Array.Empty<ProjectedBalancePreview>(), new[] { rejection.Message });
        }

        var accountIds = PostingExpander.AffectedAccountIds(expanded.Postings);
        var accounts = await _accounts.LockAccountsAsync(accountIds, ct);
        if (accounts.Count != accountIds.Count)
            return new SimulationPreview(false, "ACCOUNT_NOT_FOUND", "One or more accounts were not found.", Array.Empty<SimulationPostingPreview>(), Array.Empty<ProjectedBalancePreview>(), Array.Empty<string>());

        foreach (var account in accounts.Values)
        {
            if (account.Status != AccountStatus.Open)
                return new SimulationPreview(false, "ACCOUNT_NOT_OPEN", account.Id.ToString(), Array.Empty<SimulationPostingPreview>(), Array.Empty<ProjectedBalancePreview>(), Array.Empty<string>());
            foreach (var posting in expanded.Postings.Where(x => x.AccountId == account.Id))
            {
                if (!account.PermittedDenominations.Any(x => string.Equals(x, posting.Denomination, StringComparison.OrdinalIgnoreCase)))
                    return new SimulationPreview(false, "DENOMINATION_NOT_PERMITTED", posting.Denomination, Array.Empty<SimulationPostingPreview>(), Array.Empty<ProjectedBalancePreview>(), Array.Empty<string>());
            }
        }

        var balances = await _ledger.LoadBalancesAsync(accountIds, ct);
        if (batch.Source != BatchSource.Migration)
        {
            foreach (var account in accounts.Values.Where(x => !x.IsInternal && x.ProductVersion is not null))
            {
                var postings = expanded.Postings.Where(x => x.AccountId == account.Id).ToArray();
                var hookContext = ContractMapping.ToHookContext(account, batch.ValueTimestamp ?? DateTimeOffset.UtcNow, balances.GetValueOrDefault(account.Id, Array.Empty<Balance>()), postings);
                var contract = new ProductRuntimeContract(account.Id, account.ProductVersion!.ContractName, account.TSide, account.Parameters.ToDictionary(x => x.Name, x => x.Value));
                try
                {
                    var hookResult = await _runtime.PrePostingAsync(contract, hookContext, ct);
                    if (hookResult.Rejection is not null)
                        return new SimulationPreview(false, hookResult.Rejection.Code, hookResult.Rejection.Message, ToPostingPreview(expanded.Postings), Array.Empty<ProjectedBalancePreview>(), new[] { hookResult.Rejection.Message });
                }
                catch (Exception ex)
                {
                    await _executions.RecordAsync(account.Id, "pre_posting_simulation", EntityId.New(), contract.ContractName, "ERROR", new { error = ex.Message }, 0, ct);
                    return new SimulationPreview(false, "CONTRACT_ERROR", ex.Message, ToPostingPreview(expanded.Postings), Array.Empty<ProjectedBalancePreview>(), new[] { ex.Message });
                }
            }
        }

        var projected = ProjectBalances(expanded.Postings, balances);
        var warnings = new List<string>();
        foreach (var row in projected)
        {
            if (row.AfterRawNet < 0 && row.Phase == Phase.Committed)
                warnings.Add($"Account {row.AccountId} projected committed raw net is negative for {row.Denomination}.");
        }

        return new SimulationPreview(true, null, null, ToPostingPreview(expanded.Postings), projected, warnings);
    }

    private async Task<BatchRequest> WithDefaultSettlementAccountAsync(BatchRequest request, CancellationToken ct)
    {
        var updated = new List<InstructionRequest>();
        foreach (var instruction in request.Instructions)
        {
            if (instruction.Type is InstructionType.InboundAuth or InstructionType.OutboundAuth or InstructionType.InboundHardSettlement or InstructionType.OutboundHardSettlement
                && (!instruction.SettlementAccountId.HasValue || instruction.SettlementAccountId == Guid.Empty))
            {
                var denom = instruction.Denomination ?? "ILS";
                var settlement = await _accounts.EnsureInternalAccountAsync(LedgerConstants.SettlementSuspense, new[] { denom.ToUpperInvariant() }, ct);
                updated.Add(instruction with { SettlementAccountId = settlement });
            }
            else updated.Add(instruction);
        }
        return request with { Instructions = updated };
    }

    private static IReadOnlyList<SimulationPostingPreview> ToPostingPreview(IReadOnlyList<Posting> postings) => postings
        .Select(x => new SimulationPostingPreview(x.AccountId, x.AccountAddress, x.Asset, x.Denomination, x.Amount, x.Credit, x.Phase))
        .ToArray();

    private static IReadOnlyList<ProjectedBalancePreview> ProjectBalances(IReadOnlyList<Posting> postings, IReadOnlyDictionary<Guid, IReadOnlyList<Balance>> balances)
    {
        var map = new Dictionary<(Guid, string, string, string, Phase), (decimal Before, decimal After)>();
        foreach (var row in balances.SelectMany(x => x.Value))
        {
            var key = (row.AccountId, row.AccountAddress, row.Asset, row.Denomination, row.Phase);
            map[key] = (row.RawNet, row.RawNet);
        }
        foreach (var posting in postings)
        {
            var key = (posting.AccountId, posting.AccountAddress, posting.Asset, posting.Denomination, posting.Phase);
            if (!map.TryGetValue(key, out var value)) value = (0m, 0m);
            var delta = posting.Credit ? posting.Amount : -posting.Amount;
            map[key] = (value.Before, value.After + delta);
        }
        return map
            .Select(x => new ProjectedBalancePreview(x.Key.Item1, x.Key.Item2, x.Key.Item3, x.Key.Item4, x.Key.Item5, x.Value.Before, x.Value.After, x.Value.After - x.Value.Before))
            .OrderBy(x => x.AccountId).ThenBy(x => x.AccountAddress).ThenBy(x => x.Denomination).ThenBy(x => x.Phase)
            .ToArray();
    }

    private static SimulationDetails ToDetails(MoneySimulation simulation)
    {
        var preview = JsonSerializer.Deserialize<SimulationPreview>(simulation.PreviewJson, JsonOptions) ?? new SimulationPreview(false, "BAD_PREVIEW", "Preview could not be decoded.", Array.Empty<SimulationPostingPreview>(), Array.Empty<ProjectedBalancePreview>(), Array.Empty<string>());
        return new SimulationDetails(simulation.Id, simulation.Kind, simulation.Status, simulation.ClientId, simulation.ClientBatchId, simulation.RequestedBy, simulation.CreatedAt, simulation.ExpiresAt, simulation.ConfirmedAt, simulation.ExecutedAt, simulation.ExecutedBatchId, preview);
    }

    private static string NewToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(Guid simulationId, string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{simulationId:N}:{token}")));
    private static string HashJson(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    private static bool ConstantTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
