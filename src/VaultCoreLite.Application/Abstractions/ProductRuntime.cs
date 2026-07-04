using VaultCoreLite.Contracts;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Abstractions;

public sealed record ProductRuntimeContract(
    Guid AccountId,
    string ContractName,
    TSide TSide,
    IReadOnlyDictionary<string, string> Parameters);

public interface IProductRuntime
{
    ValueTask ValidateContractAsync(string contractName, CancellationToken ct);
    ValueTask<ProductHookResult> PrePostingAsync(ProductRuntimeContract contract, ProductHookContext context, CancellationToken ct);
    ValueTask<ProductHookResult> PostPostingAsync(ProductRuntimeContract contract, ProductHookContext context, CancellationToken ct);
    ValueTask<ProductHookResult> ScheduledEventAsync(ProductRuntimeContract contract, ProductHookContext context, string eventName, CancellationToken ct);
}
