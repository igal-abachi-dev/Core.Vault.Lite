namespace VaultCoreLite.Application.Common;

public sealed record PagedResult<TEntity>(IReadOnlyList<TEntity> Items, int TotalCount, int Page, int PageSize);
