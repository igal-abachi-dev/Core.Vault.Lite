using VaultCoreLite.Domain.Common;

namespace VaultCoreLite.Domain.Ledger;

public sealed class Product : IEntity
{
    private Product() { }
    public Product(Guid id, string name)
    {
        Id = id;
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public List<ProductVersion> Versions { get; private set; } = new();
}

public sealed class ProductVersion : IEntity
{
    private ProductVersion() { }
    public ProductVersion(Guid id, Guid productId, int version, TSide tSide, string[] denominations, string contractName, string contractVersion)
    {
        Id = id;
        ProductId = productId;
        Version = version;
        TSide = tSide;
        Denominations = denominations;
        ContractName = contractName;
        ContractVersion = contractVersion;
        Status = ProductVersionStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }
    public int Version { get; private set; }
    public TSide TSide { get; private set; }
    public string[] Denominations { get; private set; } = Array.Empty<string>();
    public string ContractName { get; private set; } = string.Empty;
    public string ContractVersion { get; private set; } = string.Empty;
    public string ParamsSchemaJson { get; private set; } = "{}";
    public string EventTypesJson { get; private set; } = "[]";
    public ProductVersionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Activate()
    {
        if (Status != ProductVersionStatus.Draft)
            throw new InvalidOperationException("Only draft product versions can be activated.");
        Status = ProductVersionStatus.Active;
    }
}

public sealed class Account : IEntity
{
    private Account() { }
    public Account(Guid id, bool isInternal, Guid? productVersionId, string[] permittedDenominations, TSide tSide)
    {
        Id = id;
        IsInternal = isInternal;
        ProductVersionId = productVersionId;
        PermittedDenominations = permittedDenominations;
        TSide = tSide;
        Status = AccountStatus.Open;
        CreatedAt = DateTimeOffset.UtcNow;
        OpenedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public bool IsInternal { get; private set; }
    public Guid? ProductVersionId { get; private set; }
    public ProductVersion? ProductVersion { get; private set; }
    public AccountStatus Status { get; private set; }
    public string[] PermittedDenominations { get; private set; } = Array.Empty<string>();
    public TSide TSide { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<AccountParameter> Parameters { get; private set; } = new();
}

public sealed class AccountParameter
{
    private AccountParameter() { }
    public AccountParameter(Guid accountId, string name, string value)
    {
        AccountId = accountId;
        Name = name;
        Value = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid AccountId { get; private set; }
    public Account? Account { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Update(string value)
    {
        Value = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class AccountParameterHistory : IEntity
{
    private AccountParameterHistory() { }
    public AccountParameterHistory(Guid id, Guid accountId, string name, string? oldValue, string newValue, string changedBy)
    {
        Id = id;
        AccountId = accountId;
        Name = name;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedBy = changedBy;
        ChangedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string NewValue { get; private set; } = string.Empty;
    public string ChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; private set; }
}

public sealed class PostingInstructionBatch : IEntity
{
    private PostingInstructionBatch() { }
    public PostingInstructionBatch(Guid id, string clientId, string clientBatchId, BatchStatus status, BatchSource source, DateTimeOffset valueTimestamp, string? rejectionCode = null, string? rejectionReason = null)
    {
        Id = id;
        ClientId = clientId;
        ClientBatchId = clientBatchId;
        Status = status;
        Source = source;
        ValueTimestamp = valueTimestamp;
        RejectionCode = rejectionCode;
        RejectionReason = rejectionReason;
        InsertedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public string ClientBatchId { get; private set; } = string.Empty;
    public BatchStatus Status { get; private set; }
    public string? RejectionCode { get; private set; }
    public string? RejectionReason { get; private set; }
    public BatchSource Source { get; private set; }
    public DateTimeOffset ValueTimestamp { get; private set; }
    public DateTimeOffset InsertedAt { get; private set; }
    public List<PostingInstruction> Instructions { get; private set; } = new();
}

public sealed class PostingInstruction : IEntity
{
    private PostingInstruction() { }
    public PostingInstruction(Guid id, Guid batchId, int seq, InstructionType type, string? clientTransactionId, decimal? amount, string? denomination, bool final)
    {
        Id = id;
        BatchId = batchId;
        Seq = seq;
        Type = type;
        ClientTransactionId = clientTransactionId;
        Amount = amount;
        Denomination = denomination;
        Final = final;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public PostingInstructionBatch? Batch { get; private set; }
    public int Seq { get; private set; }
    public InstructionType Type { get; private set; }
    public string? ClientTransactionId { get; private set; }
    public decimal? Amount { get; private set; }
    public string? Denomination { get; private set; }
    public bool Final { get; private set; }
    public List<Posting> Postings { get; private set; } = new();
}

public sealed class Posting : IEntity
{
    private Posting() { }
    public Posting(Guid id, Guid instructionId, Guid batchId, Guid accountId, string accountAddress, string asset, string denomination, decimal amount, bool credit, Phase phase, DateTimeOffset valueTimestamp)
    {
        Id = id;
        InstructionId = instructionId;
        BatchId = batchId;
        AccountId = accountId;
        AccountAddress = accountAddress;
        Asset = asset;
        Denomination = denomination;
        Amount = amount;
        Credit = credit;
        Phase = phase;
        ValueTimestamp = valueTimestamp;
        InsertedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid InstructionId { get; private set; }
    public PostingInstruction? Instruction { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid AccountId { get; private set; }
    public Account? Account { get; private set; }
    public string AccountAddress { get; private set; } = LedgerConstants.DefaultAddress;
    public string Asset { get; private set; } = LedgerConstants.DefaultAsset;
    public string Denomination { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public bool Credit { get; private set; }
    public Phase Phase { get; private set; }
    public DateTimeOffset ValueTimestamp { get; private set; }
    public DateTimeOffset InsertedAt { get; private set; }
}

public sealed class Balance
{
    private Balance() { }
    public Balance(Guid accountId, string accountAddress, string asset, string denomination, Phase phase)
    {
        AccountId = accountId;
        AccountAddress = accountAddress;
        Asset = asset;
        Denomination = denomination;
        Phase = phase;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid AccountId { get; private set; }
    public string AccountAddress { get; private set; } = LedgerConstants.DefaultAddress;
    public string Asset { get; private set; } = LedgerConstants.DefaultAsset;
    public string Denomination { get; private set; } = string.Empty;
    public Phase Phase { get; private set; }
    public decimal TotalCredits { get; private set; }
    public decimal TotalDebits { get; private set; }
    public DateTimeOffset? LastPostingAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public decimal RawNet => TotalCredits - TotalDebits;
    public decimal PresentedNet(TSide tSide) => tSide == TSide.Asset ? TotalDebits - TotalCredits : TotalCredits - TotalDebits;

    public void Apply(Posting posting)
    {
        if (posting.Credit) TotalCredits += posting.Amount;
        else TotalDebits += posting.Amount;
        LastPostingAt = posting.ValueTimestamp;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class ClientTransaction
{
    private ClientTransaction() { }
    public ClientTransaction(string clientId, string clientTransactionId, Guid accountId, Guid settlementAccountId, string denomination, ClientTransactionDirection direction, decimal authorised)
    {
        ClientId = clientId;
        ClientTransactionId = clientTransactionId;
        AccountId = accountId;
        SettlementAccountId = settlementAccountId;
        Denomination = denomination;
        Direction = direction;
        Authorised = authorised;
        Status = ClientTransactionStatus.Authorised;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string ClientId { get; private set; } = string.Empty;
    public string ClientTransactionId { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public Guid SettlementAccountId { get; private set; }
    public string Denomination { get; private set; } = string.Empty;
    public ClientTransactionDirection Direction { get; private set; }
    public decimal Authorised { get; private set; }
    public decimal Settled { get; private set; }
    public decimal Released { get; private set; }
    public ClientTransactionStatus Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public decimal Remaining => Authorised - Settled - Released;

    public void Settle(decimal amount)
    {
        if (amount > Remaining) throw new BusinessRejectionException("SETTLEMENT_EXCEEDS_AUTH", $"Settlement {amount} exceeds remaining authorised {Remaining}.");
        Settled += amount;
        RecomputeStatus();
    }

    public void ReleaseRemaining()
    {
        Released += Remaining;
        RecomputeStatus();
    }

    private void RecomputeStatus()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        var remaining = Remaining;
        if (remaining == 0 && Released > 0 && Settled == 0) Status = ClientTransactionStatus.Released;
        else if (remaining == 0 && Settled > 0) Status = ClientTransactionStatus.Settled;
        else if (Settled > 0) Status = ClientTransactionStatus.PartiallySettled;
        else Status = ClientTransactionStatus.Authorised;
    }
}

public sealed class OutboxEvent
{
    private OutboxEvent() { }
    public OutboxEvent(string topic, string key, string payloadJson)
    {
        Topic = topic;
        Key = key;
        PayloadJson = payloadJson;
        InsertedAt = DateTimeOffset.UtcNow;
    }

    public long Seq { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset InsertedAt { get; private set; }
}

public sealed class ContractExecution : IEntity
{
    private ContractExecution() { }
    public ContractExecution(Guid id, Guid? accountId, string hook, Guid triggerId, string contractName, string outcome, string detailJson, int durationMs)
    {
        Id = id;
        AccountId = accountId;
        Hook = hook;
        TriggerId = triggerId;
        ContractName = contractName;
        Outcome = outcome;
        DetailJson = detailJson;
        DurationMs = durationMs;
        InsertedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid? AccountId { get; private set; }
    public string Hook { get; private set; } = string.Empty;
    public Guid TriggerId { get; private set; }
    public string ContractName { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string DetailJson { get; private set; } = "{}";
    public int DurationMs { get; private set; }
    public DateTimeOffset InsertedAt { get; private set; }
}

public sealed class Schedule : IEntity
{
    private Schedule() { }
    public Schedule(Guid id, Guid accountId, string eventName, string cron, string timezone, DateTimeOffset nextDueAt)
    {
        Id = id;
        AccountId = accountId;
        EventName = eventName;
        Cron = cron;
        Timezone = timezone;
        NextDueAt = nextDueAt;
        Status = "ACTIVE";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Account? Account { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string Cron { get; private set; } = string.Empty;
    public string Timezone { get; private set; } = "UTC";
    public DateTimeOffset NextDueAt { get; private set; }
    public string Status { get; private set; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Advance(DateTimeOffset nextDueAt)
    {
        NextDueAt = nextDueAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class ScheduleRun : IEntity
{
    private ScheduleRun() { }
    public ScheduleRun(Guid id, Guid scheduleId, DateTimeOffset dueAt, string runnerId)
    {
        Id = id;
        ScheduleId = scheduleId;
        DueAt = dueAt;
        RunnerId = runnerId;
        Status = "RUNNING";
        StartedAt = DateTimeOffset.UtcNow;
        Attempts = 1;
    }

    public Guid Id { get; private set; }
    public Guid ScheduleId { get; private set; }
    public Schedule? Schedule { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public string RunnerId { get; private set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string Status { get; private set; } = "RUNNING";
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    public void Succeed()
    {
        Status = "SUCCEEDED";
        FinishedAt = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void Fail(string error)
    {
        Status = "FAILED";
        FinishedAt = DateTimeOffset.UtcNow;
        Error = error.Length > 2000 ? error[..2000] : error;
    }
}


public sealed class MoneySimulation : IEntity
{
    private MoneySimulation() { }

    public MoneySimulation(Guid id, SimulationKind kind, string clientId, string clientBatchId, string requestedBy, string requestHash, string requestJson, string previewJson, string confirmationTokenHash, DateTimeOffset expiresAt)
    {
        Id = id;
        Kind = kind;
        ClientId = clientId;
        ClientBatchId = clientBatchId;
        RequestedBy = requestedBy;
        RequestHash = requestHash;
        RequestJson = requestJson;
        PreviewJson = previewJson;
        ConfirmationTokenHash = confirmationTokenHash;
        ExpiresAt = expiresAt;
        Status = SimulationStatus.PendingConfirmation;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public SimulationKind Kind { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public string ClientBatchId { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string RequestJson { get; private set; } = "{}";
    public string PreviewJson { get; private set; } = "{}";
    public string ConfirmationTokenHash { get; private set; } = string.Empty;
    public SimulationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? ExecutedAt { get; private set; }
    public Guid? ExecutedBatchId { get; private set; }
    public string? RejectionCode { get; private set; }
    public string? RejectionReason { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void MarkRejected(string code, string reason)
    {
        Status = SimulationStatus.Rejected;
        RejectionCode = code;
        RejectionReason = reason;
    }

    public void MarkExpired()
    {
        Status = SimulationStatus.Expired;
        RejectionCode = "SIMULATION_EXPIRED";
        RejectionReason = "Simulation confirmation window expired.";
    }

    public void Confirm()
    {
        Status = SimulationStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }

    public void MarkExecuted(Guid batchId)
    {
        Status = SimulationStatus.Executed;
        ExecutedAt = DateTimeOffset.UtcNow;
        ExecutedBatchId = batchId;
    }
}

public sealed class SimulationConfirmationAudit : IEntity
{
    private SimulationConfirmationAudit() { }
    public SimulationConfirmationAudit(Guid id, Guid simulationId, ConfirmationStatus status, string actor, string reason)
    {
        Id = id;
        SimulationId = simulationId;
        Status = status;
        Actor = actor;
        Reason = reason;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SimulationId { get; private set; }
    public MoneySimulation? Simulation { get; private set; }
    public ConfirmationStatus Status { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
}
