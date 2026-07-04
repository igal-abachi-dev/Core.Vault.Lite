using VaultCoreLite.Domain.Common;

namespace VaultCoreLite.Domain.Ledger;

public sealed record ExpandedBatch(
    IReadOnlyList<PostingInstruction> Instructions,
    IReadOnlyList<Posting> Postings,
    IReadOnlyList<ClientTransaction> UpdatedClientTransactions);

public static class PostingExpander
{
    public static ExpandedBatch Expand(Guid batchId, BatchRequest request, IReadOnlyDictionary<string, ClientTransaction> existingClientTransactions)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId)) throw new ArgumentException("clientId is required.");
        if (string.IsNullOrWhiteSpace(request.ClientBatchId)) throw new ArgumentException("clientBatchId is required.");
        if (request.Instructions.Count == 0) throw new ArgumentException("At least one instruction is required.");

        var effectiveTime = request.ValueTimestamp ?? DateTimeOffset.UtcNow;
        var instructions = new List<PostingInstruction>();
        var postings = new List<Posting>();
        var working = existingClientTransactions.ToDictionary(x => x.Key, x => x.Value);
        var touched = new Dictionary<string, ClientTransaction>(StringComparer.Ordinal);

        for (var i = 0; i < request.Instructions.Count; i++)
        {
            var input = request.Instructions[i];
            var instructionId = EntityId.New();
            instructions.Add(new PostingInstruction(instructionId, batchId, i, input.Type, input.ClientTransactionId, input.Amount, Normalize(input.Denomination), input.Final));
            var (newPostings, updated) = ExpandOne(batchId, instructionId, request, input, working, effectiveTime);
            postings.AddRange(newPostings);
            if (updated is not null)
            {
                working[updated.ClientTransactionId] = updated;
                touched[updated.ClientTransactionId] = updated;
            }
        }

        ValidateZeroSum(postings);
        return new ExpandedBatch(instructions, postings, touched.Values.OrderBy(x => x.ClientTransactionId).ToArray());
    }

    private static (IReadOnlyList<Posting> postings, ClientTransaction? updated) ExpandOne(Guid batchId, Guid instructionId, BatchRequest request, InstructionRequest input, Dictionary<string, ClientTransaction> clientTransactions, DateTimeOffset effectiveTime)
    {
        var denom = Normalize(input.Denomination);
        var amount = input.Amount ?? 0m;
        switch (input.Type)
        {
            case InstructionType.Transfer:
                Require(input.AccountId, "TRANSFER requires accountId.");
                Require(input.TargetAccountId, "TRANSFER requires targetAccountId.");
                ValidateAmount(amount, denom);
                return (Pair(batchId, instructionId, effectiveTime, input.AccountId!.Value, input.TargetAccountId!.Value, denom, amount, creditFirst: false, Phase.Committed), null);

            case InstructionType.InboundHardSettlement:
                Require(input.AccountId, "INBOUND_HARD_SETTLEMENT requires accountId.");
                Require(input.SettlementAccountId, "INBOUND_HARD_SETTLEMENT requires settlementAccountId.");
                ValidateAmount(amount, denom);
                return (Pair(batchId, instructionId, effectiveTime, input.AccountId!.Value, input.SettlementAccountId!.Value, denom, amount, creditFirst: true, Phase.Committed), null);

            case InstructionType.OutboundHardSettlement:
                Require(input.AccountId, "OUTBOUND_HARD_SETTLEMENT requires accountId.");
                Require(input.SettlementAccountId, "OUTBOUND_HARD_SETTLEMENT requires settlementAccountId.");
                ValidateAmount(amount, denom);
                return (Pair(batchId, instructionId, effectiveTime, input.AccountId!.Value, input.SettlementAccountId!.Value, denom, amount, creditFirst: false, Phase.Committed), null);

            case InstructionType.InboundAuth:
            case InstructionType.OutboundAuth:
                Require(input.AccountId, "AUTH requires accountId.");
                Require(input.SettlementAccountId, "AUTH requires settlementAccountId.");
                if (string.IsNullOrWhiteSpace(input.ClientTransactionId)) throw new ArgumentException("AUTH requires clientTransactionId.");
                if (clientTransactions.ContainsKey(input.ClientTransactionId)) throw new ArgumentException($"Client transaction {input.ClientTransactionId} already exists.");
                ValidateAmount(amount, denom);
                var inbound = input.Type == InstructionType.InboundAuth;
                var ct = new ClientTransaction(request.ClientId, input.ClientTransactionId!, input.AccountId!.Value, input.SettlementAccountId!.Value, denom, inbound ? ClientTransactionDirection.In : ClientTransactionDirection.Out, amount);
                return (Pair(batchId, instructionId, effectiveTime, input.AccountId!.Value, input.SettlementAccountId!.Value, denom, amount, creditFirst: inbound, inbound ? Phase.PendingIn : Phase.PendingOut), ct);

            case InstructionType.Settlement:
            case InstructionType.Release:
                if (string.IsNullOrWhiteSpace(input.ClientTransactionId)) throw new ArgumentException($"{input.Type} requires clientTransactionId.");
                if (!clientTransactions.TryGetValue(input.ClientTransactionId, out var existing)) throw new KeyNotFoundException($"Client transaction {input.ClientTransactionId} not found.");
                if (input.AccountId.HasValue && input.AccountId.Value != existing.AccountId) throw new ArgumentException("Settlement/release accountId does not match original authorisation.");
                return ExpandSettlementOrRelease(batchId, instructionId, effectiveTime, input, existing);

            case InstructionType.Custom:
                if (input.Postings is null || input.Postings.Count == 0) throw new ArgumentException("CUSTOM requires postings.");
                return (input.Postings.Select(p =>
                {
                    ValidateAmount(p.Amount, Normalize(p.Denomination));
                    return new Posting(EntityId.New(), instructionId, batchId, p.AccountId,
                        string.IsNullOrWhiteSpace(p.AccountAddress) ? LedgerConstants.DefaultAddress : p.AccountAddress!,
                        string.IsNullOrWhiteSpace(p.Asset) ? LedgerConstants.DefaultAsset : p.Asset!,
                        Normalize(p.Denomination), p.Amount, p.Credit, p.Phase ?? Phase.Committed, effectiveTime);
                }).ToArray(), null);

            default:
                throw new ArgumentOutOfRangeException(nameof(input.Type), $"Unsupported instruction type {input.Type}.");
        }
    }

    private static (IReadOnlyList<Posting> postings, ClientTransaction updated) ExpandSettlementOrRelease(Guid batchId, Guid instructionId, DateTimeOffset effectiveTime, InstructionRequest input, ClientTransaction current)
    {
        var updated = Clone(current);
        var remaining = updated.Remaining < 0 ? 0 : updated.Remaining;
        var postings = new List<Posting>();
        if (input.Type == InstructionType.Release)
        {
            if (remaining == 0) return (postings, updated);
            postings.AddRange(ReversePending(batchId, instructionId, effectiveTime, updated, remaining));
            updated.ReleaseRemaining();
            return (postings, updated);
        }

        var amount = input.Final ? remaining : input.Amount ?? 0m;
        ValidateAmount(amount, current.Denomination);
        if (amount > remaining)
            throw new BusinessRejectionException("SETTLEMENT_EXCEEDS_AUTH", $"Settlement {amount} exceeds remaining authorised {remaining}.");

        postings.AddRange(ReversePending(batchId, instructionId, effectiveTime, updated, amount));
        postings.AddRange(Pair(batchId, instructionId, effectiveTime, updated.AccountId, updated.SettlementAccountId, updated.Denomination, amount, creditFirst: updated.Direction == ClientTransactionDirection.In, Phase.Committed));
        updated.Settle(amount);
        return (postings, updated);
    }

    private static ClientTransaction Clone(ClientTransaction source)
    {
        var clone = new ClientTransaction(source.ClientId, source.ClientTransactionId, source.AccountId, source.SettlementAccountId, source.Denomination, source.Direction, source.Authorised);
        if (source.Settled > 0) clone.Settle(source.Settled);
        if (source.Released > 0) clone.ReleaseRemaining();
        return clone;
    }

    private static IReadOnlyList<Posting> ReversePending(Guid batchId, Guid instructionId, DateTimeOffset effectiveTime, ClientTransaction tx, decimal amount)
    {
        var phase = tx.Direction == ClientTransactionDirection.In ? Phase.PendingIn : Phase.PendingOut;
        var creditCustomer = tx.Direction == ClientTransactionDirection.Out;
        return Pair(batchId, instructionId, effectiveTime, tx.AccountId, tx.SettlementAccountId, tx.Denomination, amount, creditCustomer, phase);
    }

    private static IReadOnlyList<Posting> Pair(Guid batchId, Guid instructionId, DateTimeOffset effectiveTime, Guid first, Guid second, string denomination, decimal amount, bool creditFirst, Phase phase)
    {
        return new[]
        {
            new Posting(EntityId.New(), instructionId, batchId, first, LedgerConstants.DefaultAddress, LedgerConstants.DefaultAsset, denomination, amount, creditFirst, phase, effectiveTime),
            new Posting(EntityId.New(), instructionId, batchId, second, LedgerConstants.DefaultAddress, LedgerConstants.DefaultAsset, denomination, amount, !creditFirst, phase, effectiveTime)
        };
    }

    public static void ValidateZeroSum(IEnumerable<Posting> postings)
    {
        var groups = postings.GroupBy(x => new { x.Asset, x.Denomination });
        foreach (var group in groups)
        {
            var credits = group.Where(x => x.Credit).Sum(x => x.Amount);
            var debits = group.Where(x => !x.Credit).Sum(x => x.Amount);
            if (credits != debits)
                throw new ArgumentException($"Batch not zero-sum for {group.Key.Asset}/{group.Key.Denomination}: credits={credits} debits={debits}.");
        }
    }

    public static IReadOnlyList<Guid> AffectedAccountIds(IEnumerable<Posting> postings) =>
        postings.Select(x => x.AccountId).Distinct().OrderBy(x => x).ToArray();

    public static IReadOnlyList<string> ReferencedClientTransactionIds(BatchRequest request) =>
        request.Instructions
            .Where(x => (x.Type == InstructionType.Settlement || x.Type == InstructionType.Release) && !string.IsNullOrWhiteSpace(x.ClientTransactionId))
            .Select(x => x.ClientTransactionId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static void Require(Guid? value, string message)
    {
        if (!value.HasValue || value.Value == Guid.Empty) throw new ArgumentException(message);
    }

    private static void ValidateAmount(decimal amount, string denomination)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(denomination)) throw new ArgumentException("Denomination is required.");
    }

    private static string Normalize(string? denomination) => (denomination ?? string.Empty).Trim().ToUpperInvariant();
}
