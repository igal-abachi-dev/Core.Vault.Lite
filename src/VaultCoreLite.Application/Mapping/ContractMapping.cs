using VaultCoreLite.Contracts;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Mapping;

public static class ContractMapping
{
    public static ProductHookContext ToHookContext(Account account, DateTimeOffset effectiveTime, IReadOnlyList<Balance> balances, IReadOnlyList<Posting> postings)
    {
        var parameters = account.Parameters.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);
        var snapshots = balances.Select(x => new ContractBalanceSnapshot(
            x.AccountAddress,
            x.Asset,
            x.Denomination,
            ToContractPhase(x.Phase),
            x.TotalCredits,
            x.TotalDebits,
            x.PresentedNet(account.TSide))).ToArray();
        var postingSnapshots = postings.Select(x => new ContractPostingSnapshot(
            x.AccountId,
            x.AccountAddress,
            x.Asset,
            x.Denomination,
            x.Amount,
            x.Credit,
            ToContractPhase(x.Phase))).ToArray();
        return new ProductHookContext(account.Id, ToContractTSide(account.TSide), effectiveTime, parameters, snapshots, postingSnapshots);
    }

    public static ContractPhase ToContractPhase(Phase phase) => phase switch
    {
        Phase.Committed => ContractPhase.Committed,
        Phase.PendingIn => ContractPhase.PendingIn,
        Phase.PendingOut => ContractPhase.PendingOut,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };

    public static Phase ToDomainPhase(ContractPhase phase) => phase switch
    {
        ContractPhase.Committed => Phase.Committed,
        ContractPhase.PendingIn => Phase.PendingIn,
        ContractPhase.PendingOut => Phase.PendingOut,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };

    public static ContractTSide ToContractTSide(TSide tSide) => tSide == TSide.Asset ? ContractTSide.Asset : ContractTSide.Liability;
}
