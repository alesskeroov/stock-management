using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Abstractions.Repositories;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Repositories;

public class EfRepository<TEntity>(StockManagementDbContext dbContext) : IGenericRepository<TEntity>
    where TEntity : class
{
    protected StockManagementDbContext DbContext { get; } = dbContext;
    protected DbSet<TEntity> Set => DbContext.Set<TEntity>();

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().SingleOrDefaultAsync(entity => EF.Property<Guid>(entity, "Id") == id, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
