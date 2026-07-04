using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VaultCoreLite.Application.Common;
using VaultCoreLite.Domain.Common;
using VaultCoreLite.Infrastructure.Persistence;

namespace VaultCoreLite.Infrastructure.Repositories;

public abstract class BaseRepository<TEntity> where TEntity : class, IEntity
{
    protected readonly VaultDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    protected BaseRepository(VaultDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    protected abstract IOrderedQueryable<TEntity> ApplyDefaultOrder(IQueryable<TEntity> query, bool descending = true);

    protected virtual IQueryable<TEntity> Query(bool tracking = false, bool ignoreQueryFilters = false)
    {
        IQueryable<TEntity> query = DbSet;
        if (ignoreQueryFilters) query = query.IgnoreQueryFilters();
        if (!tracking) query = query.AsNoTracking();
        return query;
    }

    protected IQueryable<TEntity> IncludeNavigation(IQueryable<TEntity> query, params Expression<Func<TEntity, object?>>[] navigations)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (navigations is null || navigations.Length == 0) return query;
        foreach (var navigation in navigations)
        {
            if (navigation is not null) query = query.Include(navigation);
        }
        return query;
    }

    protected virtual async Task<PagedResult<TEntity>> GetPageAsync(int page = 1, int pageSize = 50, bool descending = true, CancellationToken ct = default, params Expression<Func<TEntity, object?>>[] navigations)
    {
        IQueryable<TEntity> query = IncludeNavigation(Query(false), navigations);
        var totalCount = await query.CountAsync(ct);
        var items = await ApplyDefaultOrder(query, descending).Page(page, pageSize).ToListAsync(ct);
        return new PagedResult<TEntity>(items, totalCount, page, pageSize);
    }

    protected virtual Task<TEntity?> GetByIdAsync(Guid id, bool ignoreQueryFilters = false, CancellationToken ct = default) =>
        Query(false, ignoreQueryFilters).FirstOrDefaultAsync(entity => entity.Id == id, ct);

    protected virtual Task<TEntity?> GetTrackedByIdAsync(Guid id, bool ignoreQueryFilters = false, CancellationToken ct = default) =>
        Query(true, ignoreQueryFilters).FirstOrDefaultAsync(entity => entity.Id == id, ct);

    protected virtual Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Query(false).AnyAsync(e => e.Id == id, ct);
    protected virtual ValueTask AddAsync(TEntity entity, CancellationToken ct = default) => DbSet.AddAsync(entity, ct);
    protected virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default) => DbSet.AddRangeAsync(entities, ct);
    protected virtual void Update(TEntity entity) => DbSet.Update(entity);
    protected virtual void Delete(TEntity entity) => DbSet.Remove(entity);
}

public static class QueryableExtensions
{
    public const int MaxPageSize = 300;
    public static IQueryable<TEntity> Page<TEntity>(this IQueryable<TEntity> query, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        return query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}
