namespace VaultCoreLite.Domain.Ledger;

public sealed record PostingDraft(
    Guid AccountId,
    string? AccountAddress,
    string? Asset,
    string Denomination,
    decimal Amount,
    bool Credit,
    Phase? Phase);

public sealed record InstructionRequest(
    InstructionType Type,
    Guid? AccountId,
    Guid? TargetAccountId,
    Guid? SettlementAccountId,
    string? ClientTransactionId,
    decimal? Amount,
    string? Denomination,
    bool Final,
    IReadOnlyList<PostingDraft>? Postings);

public sealed record BatchRequest(
    string ClientId,
    string ClientBatchId,
    BatchSource Source,
    DateTimeOffset? ValueTimestamp,
    IReadOnlyList<InstructionRequest> Instructions);

public sealed record BatchResult(
    Guid Id,
    string ClientId,
    string ClientBatchId,
    BatchStatus Status,
    string? RejectionCode,
    string? RejectionReason,
    bool Replayed);

public sealed record ProductDirectivePosting(
    string? AccountRef,
    Guid? AccountId,
    string? AccountAddress,
    string? Asset,
    string Denomination,
    decimal Amount,
    bool Credit,
    Phase? Phase);

public sealed record ProductDirective(IReadOnlyList<ProductDirectivePosting> Postings);

public sealed record AuditReport(
    bool OK,
    IReadOnlyList<string> BalanceMismatches,
    IReadOnlyList<string> OutOfBalanceBatches,
    IReadOnlyDictionary<string, string> TrialBalanceByDenom,
    DateTimeOffset CheckedAt);

public sealed class BusinessRejectionException : Exception
{
    public BusinessRejectionException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}


public sealed record SimulateTransactionRequest(
    BatchRequest Batch,
    string RequestedBy,
    int? ExpiresInMinutes = null);

public sealed record SimulationPostingPreview(
    Guid AccountId,
    string AccountAddress,
    string Asset,
    string Denomination,
    decimal Amount,
    bool Credit,
    Phase Phase);

public sealed record ProjectedBalancePreview(
    Guid AccountId,
    string AccountAddress,
    string Asset,
    string Denomination,
    Phase Phase,
    decimal BeforeRawNet,
    decimal AfterRawNet,
    decimal DeltaRawNet);

public sealed record SimulationPreview(
    bool Accepted,
    string? RejectionCode,
    string? RejectionReason,
    IReadOnlyList<SimulationPostingPreview> Postings,
    IReadOnlyList<ProjectedBalancePreview> ProjectedBalances,
    IReadOnlyList<string> Warnings);

public sealed record SimulationResult(
    Guid SimulationId,
    SimulationKind Kind,
    SimulationStatus Status,
    string ClientId,
    string ClientBatchId,
    string RequestedBy,
    DateTimeOffset ExpiresAt,
    string ConfirmationToken,
    SimulationPreview Preview);

public sealed record SimulationDetails(
    Guid SimulationId,
    SimulationKind Kind,
    SimulationStatus Status,
    string ClientId,
    string ClientBatchId,
    string RequestedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExecutedAt,
    Guid? ExecutedBatchId,
    SimulationPreview Preview);

public sealed record ConfirmSimulationRequest(
    string ConfirmationToken,
    string ConfirmedBy,
    string? IdempotencyKey = null);

public sealed record ConfirmedSimulationResult(
    Guid SimulationId,
    SimulationStatus Status,
    BatchResult BatchResult);
