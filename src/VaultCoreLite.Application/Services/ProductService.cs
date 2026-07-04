using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Services;

public sealed record CreateProductRequest(string Name);
public sealed record CreateProductVersionRequest(Guid ProductId, TSide TSide, string[] Denominations, string ContractName, string ContractVersion);

public sealed class ProductService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly IProductRuntime _runtime;

    public ProductService(IProductRepository products, IUnitOfWork uow, IProductRuntime runtime)
    {
        _products = products;
        _uow = uow;
        _runtime = runtime;
    }

    public async Task<Product> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Product name is required.");
        var product = new Product(EntityId.New(), request.Name.Trim());
        await _products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);
        return product;
    }

    public async Task<ProductVersion> CreateVersionAsync(CreateProductVersionRequest request, CancellationToken ct)
    {
        await _runtime.ValidateContractAsync(request.ContractName, ct);
        var product = await _products.GetAsync(request.ProductId, ct) ?? throw new KeyNotFoundException("Product not found.");
        var versionNo = await _products.NextVersionAsync(product.Id, ct);
        var version = new ProductVersion(EntityId.New(), product.Id, versionNo, request.TSide, request.Denominations.Select(x => x.ToUpperInvariant()).ToArray(), request.ContractName, request.ContractVersion);
        await _products.AddVersionAsync(version, ct);
        await _uow.SaveChangesAsync(ct);
        return version;
    }

    public async Task ActivateVersionAsync(Guid versionId, CancellationToken ct)
    {
        var version = await _products.GetVersionAsync(versionId, ct) ?? throw new KeyNotFoundException("Product version not found.");
        version.Activate();
        await _uow.SaveChangesAsync(ct);
    }
}
