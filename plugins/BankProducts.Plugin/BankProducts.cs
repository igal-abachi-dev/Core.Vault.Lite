using System.Globalization;
using VaultCoreLite.Contracts;

namespace BankProducts.Plugin;

internal static class ProductMath
{
    public static string Denomination(ProductHookContext c) => c.Postings.FirstOrDefault()?.Denomination ?? c.Balances.FirstOrDefault()?.Denomination ?? "ILS";
    public static decimal Param(this ProductHookContext c, string name, decimal fallback = 0m) => c.DecimalParameter(name, fallback);
    public static DateTimeOffset DateParam(this ProductHookContext c, string name, DateTimeOffset fallback)
        => DateTimeOffset.TryParse(c.Parameter(name, fallback.ToString("O", CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d.ToUniversalTime() : fallback;

    public static decimal DefaultCommitted(ProductHookContext c, string denomination) => c.Balance("DEFAULT", ContractPhase.Committed, denomination);

    public static decimal ProjectedAvailableLiability(ProductHookContext c, string denomination)
    {
        var projected = c.AvailableBalance(denomination);
        foreach (var p in c.Postings.Where(p => p.AccountId == c.AccountId && p.AccountAddress.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) && p.Denomination.Equals(denomination, StringComparison.OrdinalIgnoreCase)))
        {
            if (p.Phase is ContractPhase.Committed or ContractPhase.PendingOut)
                projected += p.Credit ? p.Amount : -p.Amount;
        }
        return projected;
    }

    public static decimal ProjectedDebtAsset(ProductHookContext c, string denomination)
    {
        var projected = c.Balance("DEFAULT", ContractPhase.Committed, denomination);
        foreach (var p in c.Postings.Where(p => p.AccountId == c.AccountId && p.AccountAddress.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) && p.Denomination.Equals(denomination, StringComparison.OrdinalIgnoreCase)))
        {
            if (p.Phase is ContractPhase.Committed or ContractPhase.PendingOut)
                projected += p.Credit ? -p.Amount : p.Amount;
        }
        return projected;
    }

    public static decimal Positive(decimal x) => x > 0m ? x : 0m;
}

public sealed class CurrentAccountFactory : IProductContractFactory
{
    public string ContractName => "CurrentAccount";
    public IProductContract Create() => new CurrentAccountContract();
}

public sealed class CurrentAccountContract : IProductContract
{
    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        var overdraftLimit = c.Param("overdraft_limit", 0m);
        var projected = ProductMath.ProjectedAvailableLiability(c, denom);
        return projected < -overdraftLimit
            ? ValueTask.FromResult(ProductHookResult.Reject("INSUFFICIENT_FUNDS", $"Would exceed overdraft limit. Projected available {projected} {denom}."))
            : ValueTask.FromResult(ProductHookResult.Accepted);
    }

    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var fee = c.Param("transaction_fee", 0m);
        if (fee <= 0m || c.Postings.Count == 0) return ValueTask.FromResult(ProductHookResult.Accepted);
        var denom = ProductMath.Denomination(c);
        return ValueTask.FromResult(ProductHookResult.WithDirectives(
            ProductPostingDirective.Debit("self", fee, denom),
            ProductPostingDirective.Credit("FEE_INCOME", fee, denom)));
    }

    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext c, string eventName, CancellationToken ct)
    {
        if (!eventName.Equals("MONTHLY_ACCOUNT_FEE", StringComparison.OrdinalIgnoreCase)) return ValueTask.FromResult(ProductHookResult.Accepted);
        var fee = c.Param("monthly_fee", 0m);
        if (fee <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
        var denom = ProductMath.Denomination(c);
        return ValueTask.FromResult(ProductHookResult.WithDirectives(
            ProductPostingDirective.Debit("self", fee, denom),
            ProductPostingDirective.Credit("FEE_INCOME", fee, denom)));
    }
}

public sealed class SavingsAccountFactory : IProductContractFactory
{
    public string ContractName => "SavingsAccount";
    public IProductContract Create() => new SavingsAccountContract();
}

public sealed class SavingsAccountContract : IProductContract
{
    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        var min = c.Param("min_balance", 0m);
        var projected = ProductMath.ProjectedAvailableLiability(c, denom);
        return projected < min
            ? ValueTask.FromResult(ProductHookResult.Reject("INSUFFICIENT_FUNDS", $"Savings minimum balance would be breached. Projected {projected} {denom}."))
            : ValueTask.FromResult(ProductHookResult.Accepted);
    }

    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext c, CancellationToken ct) => ValueTask.FromResult(ProductHookResult.Accepted);

    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext c, string eventName, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        if (eventName.Equals("ACCRUE_INTEREST", StringComparison.OrdinalIgnoreCase))
        {
            var annualRate = c.Param("annual_rate", 0.03m);
            var balance = ProductMath.Positive(ProductMath.DefaultCommitted(c, denom));
            var accrual = Math.Round(balance * annualRate / 365m, 9, MidpointRounding.AwayFromZero);
            if (accrual <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
            return ValueTask.FromResult(ProductHookResult.WithDirectives(
                ProductPostingDirective.Debit("INTEREST_EXPENSE", accrual, denom),
                ProductPostingDirective.Credit("self", accrual, denom, "ACCRUED_INTEREST")));
        }
        if (eventName.Equals("APPLY_INTEREST", StringComparison.OrdinalIgnoreCase))
        {
            var accrued = ProductMath.Positive(c.Balance("ACCRUED_INTEREST", ContractPhase.Committed, denom));
            if (accrued <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
            return ValueTask.FromResult(ProductHookResult.WithDirectives(
                ProductPostingDirective.Debit("self", accrued, denom, "ACCRUED_INTEREST"),
                ProductPostingDirective.Credit("self", accrued, denom)));
        }
        return ValueTask.FromResult(ProductHookResult.Accepted);
    }
}

public sealed class TermDepositFactory : IProductContractFactory
{
    public string ContractName => "TermDeposit";
    public IProductContract Create() => new TermDepositContract();
}

public sealed class TermDepositContract : IProductContract
{
    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        var maturity = c.DateParam("maturity_date", DateTimeOffset.MaxValue);
        var early = c.Parameter("allow_early_withdrawal", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        var outgoing = c.Postings.Any(p => p.AccountId == c.AccountId && p.AccountAddress == "DEFAULT" && p.Denomination == denom && !p.Credit);
        if (outgoing && c.EffectiveTime < maturity && !early)
            return ValueTask.FromResult(ProductHookResult.Reject("TERM_DEPOSIT_LOCKED", $"Term deposit matures at {maturity:O}."));
        return ValueTask.FromResult(ProductHookResult.Accepted);
    }

    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext c, CancellationToken ct) => ValueTask.FromResult(ProductHookResult.Accepted);

    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext c, string eventName, CancellationToken ct)
    {
        if (!eventName.Equals("MATURITY_INTEREST", StringComparison.OrdinalIgnoreCase)) return ValueTask.FromResult(ProductHookResult.Accepted);
        var denom = ProductMath.Denomination(c);
        var principal = ProductMath.Positive(ProductMath.DefaultCommitted(c, denom));
        var rate = c.Param("term_rate", 0m);
        var interest = Math.Round(principal * rate, 2, MidpointRounding.AwayFromZero);
        if (interest <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
        return ValueTask.FromResult(ProductHookResult.WithDirectives(
            ProductPostingDirective.Debit("INTEREST_EXPENSE", interest, denom),
            ProductPostingDirective.Credit("self", interest, denom)));
    }
}

public sealed class WalletFactory : IProductContractFactory
{
    public string ContractName => "Wallet";
    public IProductContract Create() => new WalletContract();
}

public sealed class WalletContract : IProductContract
{
    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        var projected = ProductMath.ProjectedAvailableLiability(c, denom);
        return projected < 0m ? ValueTask.FromResult(ProductHookResult.Reject("INSUFFICIENT_FUNDS", "Wallet cannot go negative.")) : ValueTask.FromResult(ProductHookResult.Accepted);
    }
    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext c, CancellationToken ct) => ValueTask.FromResult(ProductHookResult.Accepted);
    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext c, string eventName, CancellationToken ct) => ValueTask.FromResult(ProductHookResult.Accepted);
}

public sealed class PersonalLoanFactory : IProductContractFactory
{
    public string ContractName => "PersonalLoan";
    public IProductContract Create() => new LoanContract("PersonalLoan");
}

public sealed class MortgageLoanFactory : IProductContractFactory
{
    public string ContractName => "MortgageLoan";
    public IProductContract Create() => new LoanContract("MortgageLoan");
}

public sealed class LoanContract : IProductContract
{
    private readonly string _name;
    public LoanContract(string name) => _name = name;

    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        var principalLimit = c.Param("principal_limit", decimal.MaxValue / 4m);
        var projectedDebt = ProductMath.ProjectedDebtAsset(c, denom);
        return projectedDebt > principalLimit
            ? ValueTask.FromResult(ProductHookResult.Reject("LOAN_LIMIT_EXCEEDED", $"{_name} projected balance {projectedDebt} exceeds limit {principalLimit}."))
            : ValueTask.FromResult(ProductHookResult.Accepted);
    }

    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext c, CancellationToken ct) => ValueTask.FromResult(ProductHookResult.Accepted);

    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext c, string eventName, CancellationToken ct)
    {
        if (!eventName.Equals("ACCRUE_INTEREST", StringComparison.OrdinalIgnoreCase)) return ValueTask.FromResult(ProductHookResult.Accepted);
        var denom = ProductMath.Denomination(c);
        var debt = ProductMath.Positive(c.Balance("DEFAULT", ContractPhase.Committed, denom));
        var annualRate = c.Param("annual_rate", 0.08m);
        var interest = Math.Round(debt * annualRate / 365m, 9, MidpointRounding.AwayFromZero);
        if (interest <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
        return ValueTask.FromResult(ProductHookResult.WithDirectives(
            ProductPostingDirective.Debit("self", interest, denom),
            ProductPostingDirective.Credit("INTEREST_INCOME", interest, denom)));
    }
}

public sealed class CreditCardFactory : IProductContractFactory
{
    public string ContractName => "CreditCard";
    public IProductContract Create() => new CreditCardContract();
}

public sealed class CreditCardContract : IProductContract
{
    public ValueTask<ProductHookResult> PrePostingAsync(ProductHookContext c, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        var limit = c.Param("credit_limit", 0m);
        var projectedDebt = ProductMath.ProjectedDebtAsset(c, denom);
        return projectedDebt > limit
            ? ValueTask.FromResult(ProductHookResult.Reject("CREDIT_LIMIT_EXCEEDED", $"Projected card debt {projectedDebt} exceeds limit {limit}."))
            : ValueTask.FromResult(ProductHookResult.Accepted);
    }

    public ValueTask<ProductHookResult> PostPostingAsync(ProductHookContext c, CancellationToken ct) => ValueTask.FromResult(ProductHookResult.Accepted);

    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductHookContext c, string eventName, CancellationToken ct)
    {
        var denom = ProductMath.Denomination(c);
        if (eventName.Equals("ANNUAL_FEE", StringComparison.OrdinalIgnoreCase))
        {
            var fee = c.Param("annual_fee", 0m);
            if (fee <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
            return ValueTask.FromResult(ProductHookResult.WithDirectives(
                ProductPostingDirective.Debit("self", fee, denom),
                ProductPostingDirective.Credit("FEE_INCOME", fee, denom)));
        }
        if (eventName.Equals("ACCRUE_INTEREST", StringComparison.OrdinalIgnoreCase))
        {
            var debt = ProductMath.Positive(c.Balance("DEFAULT", ContractPhase.Committed, denom));
            var apr = c.Param("apr", 0.24m);
            var interest = Math.Round(debt * apr / 365m, 9, MidpointRounding.AwayFromZero);
            if (interest <= 0m) return ValueTask.FromResult(ProductHookResult.Accepted);
            return ValueTask.FromResult(ProductHookResult.WithDirectives(
                ProductPostingDirective.Debit("self", interest, denom),
                ProductPostingDirective.Credit("INTEREST_INCOME", interest, denom)));
        }
        return ValueTask.FromResult(ProductHookResult.Accepted);
    }
}
