using VaultCoreLite.Application.Abstractions;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Application.Services;

public sealed record CreateAccountRequest(bool IsInternal, Guid? ProductVersionId, string[] PermittedDenominations, IReadOnlyDictionary<string, string>? Parameters);

public sealed class AccountService
{
    private readonly IAccountRepository _accounts;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;

    public AccountService(IAccountRepository accounts, IProductRepository products, IUnitOfWork uow)
    {
        _accounts = accounts;
        _products = products;
        _uow = uow;
    }

    public async Task<Account> CreateAsync(CreateAccountRequest request, string changedBy, CancellationToken ct)
    {
        if (request.PermittedDenominations.Length == 0) throw new ArgumentException("permittedDenominations is required.");
        if (request.IsInternal && request.ProductVersionId is not null) throw new ArgumentException("Internal account cannot have a productVersionId.");
        if (!request.IsInternal && request.ProductVersionId is null) throw new ArgumentException("Customer account requires productVersionId.");

        var tside = TSide.Liability;
        if (request.ProductVersionId is not null)
        {
            var pv = await _products.GetVersionAsync(request.ProductVersionId.Value, ct) ?? throw new KeyNotFoundException("Product version not found.");
            if (pv.Status != ProductVersionStatus.Active) throw new InvalidOperationException("Product version is not ACTIVE.");
            tside = pv.TSide;
        }

        var account = new Account(EntityId.New(), request.IsInternal, request.ProductVersionId, request.PermittedDenominations.Select(x => x.ToUpperInvariant()).ToArray(), tside);
        await _accounts.AddAsync(account, ct);
        if (request.Parameters is { Count: > 0 }) await _accounts.SetParametersAsync(account.Id, request.Parameters, changedBy, ct);
        await _uow.SaveChangesAsync(ct);
        return account;
    }

    public Task<Account?> GetAsync(Guid id, CancellationToken ct) => _accounts.GetAsync(id, ct);
}
