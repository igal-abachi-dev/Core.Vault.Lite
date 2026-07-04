using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VaultCoreLite.Infrastructure.Persistence;

public sealed class VaultDbContextFactory : IDesignTimeDbContextFactory<VaultDbContext>
{
    public VaultDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("VAULTLITE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=vaultlite;Username=vaultlite_app;Password=vaultlite_app";
        var options = new DbContextOptionsBuilder<VaultDbContext>().UseNpgsql(cs).Options;
        return new VaultDbContext(options);
    }
}
