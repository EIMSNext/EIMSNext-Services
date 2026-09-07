using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// Mongo 实体服务基类，为 <typeparamref name="T"/> 实体提供 <see cref="IService{T}"/> 的默认实现。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    public abstract class MongoEntityServiceBase<T> : ServiceCore<T>, IService<T> where T : class, IMongoEntity
    {
        #region Variables

        #endregion 

        /// <summary>
        /// 初始化 <see cref="MongoEntityServiceBase{T}"/> 类的新实例。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        public MongoEntityServiceBase(IResolver resolver)
            : base(resolver)
        {
        }

        #region Properties

        /// <summary>
        /// 获取实体对应的 Mongo 集合。
        /// </summary>
        public IMongoCollection<T> Collection => Repository.Collection;

        #endregion

        #region Methods

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public T? Get(string id)
        {
            return GetCore(id, null);
        }

        /// <summary>
        /// 获取全部实体的可查询对象。
        /// </summary>
        /// <returns>实体的可查询对象。</returns>
        public IQueryable<T> All()
        {
            return Repository.Queryable;
        }

        /// <summary>
        /// 根据表达式过滤条件查询实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>实体的可查询对象。</returns>
        public IQueryable<T> Query(Expression<Func<T, bool>> where)
        {
            return Repository.Queryable.Where(where);
        }

        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        public IFindFluent<T, T> Find(DynamicFindOptions<T> options)
        {
            return FindCore(options, null);
        }

        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter)
        {
            return FindCore(filter, null);
        }

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        public long Count(DynamicFilter filter)
        {
            return CountCore(filter);
        }

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        public long Count(Expression<Func<T, bool>> filter)
        {
            return CountCore(filter);
        }

        /// <summary>
        /// 判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public bool Exists(Expression<Func<T, bool>> where)
        {
            return ExistsCore(where, null);
        }

        /// <summary>
        /// 判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public bool Exists(DynamicFilter where)
        {
            return ExistsCore(where, null);
        }

        /// <summary>
        /// 新增单个实体。
        /// </summary>
        /// <param name="entity">要新增的实体。</param>
        public virtual void Add(T entity)
        {
            Add(new List<T>() { entity });
        }

        /// <summary>
        /// 批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        public virtual void Add(IEnumerable<T> entities)
        {
            using (var scope = NewTransactionScope())
            {
                AddCore(entities, scope.SessionHandle);
                scope.CommitTransaction();
            }
        }

        /// <summary>
        /// 替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>替换结果。</returns>
        public virtual ReplaceOneResult Replace(T entity)
        {
            using (var scope = NewTransactionScope())
            {
                var result = ReplaceCore(entity, scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        /// <summary>
        /// 根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>删除结果。</returns>
        public virtual object Delete(string id)
        {
            using (var scope = NewTransactionScope())
            {
                var result = DeleteCore(FilterBuilder.Eq(x => x.Id, id), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        /// <summary>
        /// 根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <returns>删除结果。</returns>
        public virtual object Delete(IEnumerable<string> ids)
        {
            using (var scope = NewTransactionScope())
            {
                var result = DeleteCore(FilterBuilder.In(x => x.Id, ids), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        /// <summary>
        /// 根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>删除结果。</returns>
        public virtual object Delete(DynamicFilter filter)
        {
            using (var scope = NewTransactionScope())
            {
                var result = DeleteCore(filter.ToFilterDefinition<T>(), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        #endregion

        #region Async Methods

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public Task<T?> GetAsync(string id)
        {
            return GetCoreAsync(id, null);
        }

        /// <summary>
        /// 异步根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>异步游标。</returns>
        public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options)
        {
            return FindCoreAsync(options, null);
        }

        /// <summary>
        /// 异步根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>异步游标。</returns>
        public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter)
        {
            return FindCoreAsync(filter, null);
        }

        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        public Task<long> CountAsync(DynamicFilter filter)
        {
            return CountCoreAsync(filter);
        }

        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        public Task<long> CountAsync(Expression<Func<T, bool>> filter)
        {
            return CountCoreAsync(filter);
        }

        /// <summary>
        /// 异步判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public Task<bool> ExistsAsync(Expression<Func<T, bool>> where)
        {
            return ExistsCoreAsync(where, null);
        }

        /// <summary>
        /// 异步判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public Task<bool> ExistsAsync(DynamicFilter where)
        {
            return ExistsCoreAsync(where, null);
        }

        /// <summary>
        /// 异步新增单个实体。
        /// </summary>
        /// <param name="entity">要新增的实体。</param>
        public virtual Task AddAsync(T entity)
        {
            return AddAsync(new List<T> { entity });
        }

        /// <summary>
        /// 异步批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        public virtual async Task AddAsync(IEnumerable<T> entities)
        {
            using (var scope = NewTransactionScope())
            {
                await AddCoreAsync(entities, scope.SessionHandle);
                scope.CommitTransaction();
                return;
            }
        }

        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>替换结果。</returns>
        public virtual async Task<ReplaceOneResult> ReplaceAsync(T entity)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await ReplaceCoreAsync(entity, scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>删除结果。</returns>
        public virtual async Task<object> DeleteAsync(string id)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await DeleteCoreAsync(FilterBuilder.Eq(x =>x.Id, id), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <returns>删除结果。</returns>
        public virtual async Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await DeleteCoreAsync(FilterBuilder.In(x => x.Id, ids), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>删除结果。</returns>
        public virtual async Task<object> DeleteAsync(DynamicFilter filter)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await DeleteCoreAsync(filter.ToFilterDefinition<T>(), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        #endregion
    }
}
