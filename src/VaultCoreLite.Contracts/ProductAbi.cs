namespace VaultCoreLite.Contracts;

public enum ContractTSide
{
    Asset,
    Liability
}

public enum ContractPhase
{
    Committed,
    PendingIn,
    PendingOut
}

public sealed record ContractBalanceSnapshot(
    string AccountAddress,
    string Asset,
    string Denomination,
    ContractPhase Phase,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal PresentedNet);

public sealed record ContractPostingSnapshot(
    Guid AccountId,
    string AccountAddress,
    string Asset,
    string Denomination,
    decimal Amount,
    bool Credit,
    ContractPhase Phase);

public sealed record ProductHookContext(
    Guid AccountId,
    ContractTSide TSide,
    DateTimeOffset EffectiveTime,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<ContractBalanceSnapshot> Balances,
    IReadOnlyList<ContractPostingSnapshot> Postings)
{
    public decimal Balance(string address, ContractPhase phase, string denomination)
    {
        return Balances
            .Where(x => string.Equals(x.AccountAddress, address, StringComparison.OrdinalIgnoreCase)
                     && x.Phase == phase
                     && string.Equals(x.Denomination, denomination, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.PresentedNet);
    }

    public decimal AvailableBalance(string denomination)
    {
        var committed = Balance("DEFAULT", ContractPhase.Committed, denomination);
        var pendingOut = Balance("DEFAULT", ContractPhase.PendingOut, denomination);
        return committed - Math.Abs(pendingOut);
    }

    public string Parameter(string name, string fallback = "0")
    {
        return Parameters.TryGetValue(name, out var value) ? value : fallback;
    }

    public decimal DecimalParameter(string name, decimal fallback = 0m)
    {
        return decimal.TryParse(Parameter(name, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }
}

public sealed record ProductRejection(string Code, string Message);

public sealed record ProductPostingDirective(
    string? AccountRef,
    Guid? AccountId,
    string AccountAddress,
    string Asset,
    string Denomination,
    decimal Amount,
    bool Credit,
    ContractPhase Phase)
{
    public static ProductPostingDirective Debit(string accountRef, decimal amount, string denomination, string address = "DEFAULT") =>
        new(accountRef, null, address, "COMMERCIAL_BANK_MONEY", denomination, amount, false, ContractPhase.Committed);

    public static ProductPostingDirective Credit(string accountRef, decimal amount, string denomination, string address = "DEFAULT") =>
        new(accountRef, null, address, "COMMERCIAL_BANK_MONEY", denomination, amount, true, ContractPhase.Committed);
}

public sealed record ProductHookResult(ProductRejection? Rejection, IReadOnlyList<ProductPostingDirective> Directives)
{
    public static ProductHookResult Accepted { get; } = new(null, Array.Empty<ProductPostingDirective>());
    public static ProductHookResult Reject(string code, string message) => new(new ProductRejection(code, message), Array.Empty<ProductPostingDirective>());
    public static ProductHookResult WithDirectives(params ProductPostingDirective[] directives) => new(null, directives);
}

public interface IProductContract
{
    ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext context, CancellationToken cancellationToken);
    ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext context, CancellationToken cancellationToken);
    ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext context, string eventName, CancellationToken cancellationToken);
}

public interface IProductContractFactory
{
    string ContractName { get; }
    IProductContract Create();
}
