using System.Diagnostics;
using System.Reflection;
using System.Security;
using Microsoft.Extensions.Options;
using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Contracts;

namespace VaultCoreLite.ProductRuntime.Plugin;

public sealed class PluginProductRuntime : IProductRuntime
{
    private readonly ProductRuntimeOptions _options;
    private readonly Lazy<IReadOnlyDictionary<string, IProductContractFactory>> _factories;

    public PluginProductRuntime(IOptions<ProductRuntimeOptions> options)
    {
        _options = options.Value;
        _factories = new Lazy<IReadOnlyDictionary<string, IProductContractFactory>>(LoadFactories, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ValueTask ValidateContractAsync(string contractName, CancellationToken ct)
    {
        if (!_factories.Value.ContainsKey(contractName)) throw new InvalidOperationException($"Product contract '{contractName}' was not found in plugin '{_options.PluginPath}'.");
        return ValueTask.CompletedTask;
    }

    public ValueTask<ProductHookResult> PrePostingAsync(ProductRuntimeContract contract, ProductHookContext context, CancellationToken ct) => Get(contract.ContractName).PrePostingAsync(context, ct);
    public ValueTask<ProductHookResult> PostPostingAsync(ProductRuntimeContract contract, ProductHookContext context, CancellationToken ct) => Get(contract.ContractName).PostPostingAsync(context, ct);
    public ValueTask<ProductHookResult> ScheduledEventAsync(ProductRuntimeContract contract, ProductHookContext context, string eventName, CancellationToken ct) => Get(contract.ContractName).ScheduledEventAsync(context, eventName, ct);

    private IProductContract Get(string contractName) => _factories.Value.TryGetValue(contractName, out var factory) ? factory.Create() : throw new InvalidOperationException($"Product contract '{contractName}' was not loaded.");

    private IReadOnlyDictionary<string, IProductContractFactory> LoadFactories()
    {
        var fullPath = Path.GetFullPath(_options.PluginPath, AppContext.BaseDirectory);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"Plugin not found: {fullPath}", fullPath);
        if (_options.RequireAuthenticode) VerifyAuthenticodeOrThrow(fullPath);

        var alc = new PluginLoadContext(fullPath);
        var asm = alc.LoadFromAssemblyPath(fullPath);
        var factoryTypes = asm.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IProductContractFactory).IsAssignableFrom(t));

        if (!string.IsNullOrWhiteSpace(_options.FactoryType))
            factoryTypes = factoryTypes.Where(x => string.Equals(x.FullName, _options.FactoryType, StringComparison.Ordinal));

        var factories = factoryTypes
            .Select(t => (IProductContractFactory)Activator.CreateInstance(t)!)
            .ToDictionary(x => x.ContractName, StringComparer.Ordinal);

        if (factories.Count == 0) throw new InvalidOperationException("No IProductContractFactory implementation was found in the plugin.");
        return factories;
    }

    private void VerifyAuthenticodeOrThrow(string path)
    {
        using var p = new Process();
        p.StartInfo.FileName = _options.SigntoolPath;
        p.StartInfo.Arguments = $"verify /pa /all /v \"{path}\"";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.Start();
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        if (p.ExitCode != 0) throw new SecurityException($"Authenticode verification failed for {path}\n{stderr}\n{stdout}");
        if (!string.IsNullOrWhiteSpace(_options.RequiredSignerOutputContains) && !stdout.Contains(_options.RequiredSignerOutputContains, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException($"Authenticode verification succeeded, but expected signer marker '{_options.RequiredSignerOutputContains}' was not found.");
    }
}
