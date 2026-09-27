using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;

namespace EIMSNext.Core.Mongo.Repositories
{
    /// <summary>
    /// <typeparamref name="T"/> 实体的默认 Mongo 仓储实现。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    public class DbRepository<T> : RepositoryBase<T> where T : class, IMongoEntity
    {
        #region Variables

        #endregion

        /// <summary>
        /// 初始化 <see cref="DbRepository{T}"/> 类的新实例。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        public DbRepository(IMongoDbContex dbContext)
            : base(dbContext)
        {
        }

        #region Properties

        #endregion

        #region Methods       

        #endregion

        #region Async Methods

        #endregion

        #region Helper

        #endregion
    }
}
