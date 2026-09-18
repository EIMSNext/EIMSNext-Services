using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Mongo.Repositories;

public abstract class RepositoryBase<T> : IRepository<T> where T : class, IMongoEntity
{
    protected readonly DbContext Context;
    protected RepositoryBase(DbContext context) => Context = context;
    public IQueryable<T> Queryable => Context.Set<T>().AsNoTracking();
    public T? Get(string id) => Queryable.FirstOrDefault(x => x.Id == id);
    public Task<T?> GetAsync(string id, CancellationToken cancellationToken = default) => Queryable.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public long Count(Expression<Func<T, bool>> predicate) => Queryable.LongCount(predicate);
    public Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => Queryable.LongCountAsync(predicate, cancellationToken);
    public void Insert(T entity) { Context.Set<T>().Add(entity); Context.SaveChanges(); }
    public async Task InsertAsync(T entity, CancellationToken cancellationToken = default) { await Context.Set<T>().AddAsync(entity, cancellationToken); await Context.SaveChangesAsync(cancellationToken); }
    public async Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) { await Context.Set<T>().AddRangeAsync(entities, cancellationToken); await Context.SaveChangesAsync(cancellationToken); }
    public void Replace(T entity) { Context.Set<T>().Update(entity); Context.SaveChanges(); }
    public async Task ReplaceAsync(T entity, CancellationToken cancellationToken = default) { Context.Set<T>().Update(entity); await Context.SaveChangesAsync(cancellationToken); }
    public void Delete(T entity) { Context.Set<T>().Remove(entity); Context.SaveChanges(); }
    public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default) { Context.Set<T>().Remove(entity); await Context.SaveChangesAsync(cancellationToken); }
    public string NewId() => Guid.NewGuid().ToString("N");
}
