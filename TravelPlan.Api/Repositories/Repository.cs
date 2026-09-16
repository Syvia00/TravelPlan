using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;

namespace TravelPlan.Api.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly TravelPlanDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public Repository(TravelPlanDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public Task<List<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        DbSet.ToListAsync(cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await DbSet.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => DbSet.Update(entity);

    public void Remove(TEntity entity) => DbSet.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Context.SaveChangesAsync(cancellationToken);
}
