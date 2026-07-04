using VaultCoreLite.Contracts;

namespace SavingsProduct.Plugin;

public sealed class SavingsProductFactory : IProductContractFactory
{
    public string ContractName => "SavingsProduct";
    public IProductContract Create() => new SavingsProductContract();
}

public sealed class SavingsProductContract : IProductContract
{
    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext context, CancellationToken cancellationToken)
    {
        foreach (var posting in context.Postings)
        {
            if (!string.Equals(posting.Denomination, "ILS", StringComparison.OrdinalIgnoreCase))
                return new(ProductHookResult.Reject("WRONG_DENOMINATION", "SavingsProduct supports ILS only."));
        }

        var minBalance = context.DecimalParameter("min_balance", 0m);
        var projectedDefaultCommitted = context.AvailableBalance("ILS") + BatchNet(context.Postings, "DEFAULT", ContractPhase.Committed, "ILS");
        if (projectedDefaultCommitted < minBalance)
            return new(ProductHookResult.Reject("INSUFFICIENT_FUNDS", "Posting would breach the minimum balance."));

        return new(ProductHookResult.Accepted);
    }

    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext context, CancellationToken cancellationToken)
    {
        var fee = context.DecimalParameter("transaction_fee", 0m);
        if (fee <= 0) return new(ProductHookResult.Accepted);

        return new(ProductHookResult.WithDirectives(
            ProductPostingDirective.Debit("self", fee, "ILS"),
            ProductPostingDirective.Credit("FEE_INCOME", fee, "ILS")));
    }

    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext context, string eventName, CancellationToken cancellationToken)
    {
        if (!string.Equals(eventName, "ACCRUE_INTEREST", StringComparison.OrdinalIgnoreCase))
            return new(ProductHookResult.Accepted);

        var balance = context.Balance("DEFAULT", ContractPhase.Committed, "ILS");
        var annualRate = context.DecimalParameter("gross_annual_rate", 0m);
        var accrual = Math.Round(balance * annualRate / 365m, 9, MidpointRounding.AwayFromZero);
        if (accrual <= 0) return new(ProductHookResult.Accepted);

        return new(ProductHookResult.WithDirectives(
            ProductPostingDirective.Debit("INTEREST_EXPENSE", accrual, "ILS"),
            ProductPostingDirective.Credit("self", accrual, "ILS", "ACCRUED_INTEREST")));
    }

    private static decimal BatchNet(IEnumerable<ContractPostingSnapshot> postings, string address, ContractPhase phase, string denomination)
    {
        return postings
            .Where(x => string.Equals(x.AccountAddress, address, StringComparison.OrdinalIgnoreCase)
                     && x.Phase == phase
                     && string.Equals(x.Denomination, denomination, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Credit ? x.Amount : -x.Amount);
    }
}
