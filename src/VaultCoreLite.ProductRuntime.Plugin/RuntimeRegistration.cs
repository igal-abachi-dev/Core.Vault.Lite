using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VaultCoreLite.Application.Abstractions;

namespace VaultCoreLite.ProductRuntime.Plugin;

public static class RuntimeRegistration
{
    public static IServiceCollection AddProductRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProductRuntimeOptions>()
            .Bind(configuration.GetSection("ProductRuntime"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddKeyedScoped<IProductRuntime, StubProductRuntime>("stub");
        services.AddKeyedScoped<IProductRuntime, PluginProductRuntime>("plugin");
        services.AddScoped<IProductRuntime>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProductRuntimeOptions>>().Value;
            var key = string.Equals(options.Mode, "Plugin", StringComparison.OrdinalIgnoreCase) ? "plugin" : "stub";
            return sp.GetRequiredKeyedService<IProductRuntime>(key);
        });
        return services;
    }
}
