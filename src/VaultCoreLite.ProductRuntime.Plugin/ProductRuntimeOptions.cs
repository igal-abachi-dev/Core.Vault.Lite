using System.ComponentModel.DataAnnotations;

namespace VaultCoreLite.ProductRuntime.Plugin;

public sealed class ProductRuntimeOptions
{
    [Required]
    [RegularExpression("^(Stub|Plugin)$")]
    public string Mode { get; set; } = "Stub";

    public string PluginPath { get; set; } = "plugins/BankProducts.Plugin.dll";
    public string? FactoryType { get; set; }
    public bool RequireAuthenticode { get; set; }
    public string SigntoolPath { get; set; } = "signtool";
    public string? RequiredSignerOutputContains { get; set; }
}
