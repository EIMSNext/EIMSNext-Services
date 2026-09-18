using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Mongo.Repositories;

public interface IRepository<T> where T : class, IMongoEntity
{
    IQueryable<T> Queryable { get; }
    T? Get(string id);
    Task<T?> GetAsync(string id, CancellationToken cancellationToken = default);
    long Count(Expression<Func<T, bool>> predicate);
    Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    void Insert(T entity);
    Task InsertAsync(T entity, CancellationToken cancellationToken = default);
    Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Replace(T entity);
    Task ReplaceAsync(T entity, CancellationToken cancellationToken = default);
    void Delete(T entity);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    string NewId();
}
