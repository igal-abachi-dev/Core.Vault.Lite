using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Contracts;

namespace VaultCoreLite.ProductRuntime.Plugin;

public sealed class StubProductRuntime : IProductRuntime
{
    public ValueTask ValidateContractAsync(string contractName, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask<ProductHookResult> PrePostingAsync(ProductRuntimeContract contract, ProductHookContext context, CancellationToken ct) => new(ProductHookResult.Accepted);
    public ValueTask<ProductHookResult> PostPostingAsync(ProductRuntimeContract contract, ProductHookContext context, CancellationToken ct) => new(ProductHookResult.Accepted);
    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductRuntimeContract contract, ProductHookContext context, string eventName, CancellationToken ct) => new(ProductHookResult.Accepted);
}
