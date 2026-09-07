using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 实体服务基类，为包含审计字段的 <typeparamref name="T"/> 实体提供系统字段填充逻辑。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntity"/> 的实体类型。</typeparam>
    public abstract class EntityServiceBase<T> : MongoEntityServiceBase<T>, IService<T> where T : class, IEntity
    {
        #region Variables

        #endregion 

        /// <summary>
        /// 初始化 <see cref="EntityServiceBase{T}"/> 类的新实例。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        public EntityServiceBase(IResolver resolver)
            : base(resolver)
        {
        }

        /// <summary>
        /// 填充实体的系统字段（创建人、更新时间、企业 ID 等）。
        /// </summary>
        /// <param name="entity">要填充的实体。</param>
        /// <param name="isEdit">是否为编辑操作。</param>
        /// <returns>填充后的实体。</returns>
        protected override T FillSystemField(T entity, bool isEdit)
        {
            if (isEdit)
            {
                entity.UpdateBy = Context.Operator;
                entity.UpdateTime = DateTime.UtcNow.ToTimeStampMs();
            }
            else
            {
                entity.CreateBy = Context.Operator;
                entity.UpdateBy = entity.CreateBy;
                entity.CreateTime = DateTime.UtcNow.ToTimeStampMs();
                entity.UpdateTime = entity.CreateTime;

                if (!string.IsNullOrEmpty(Context.CorpId) && ICorpOwnedType.IsAssignableFrom(typeof(T)))
                {
                    var ownedEntity = (entity as ICorpOwned)!;
                    if (string.IsNullOrEmpty(ownedEntity.CorpId))
                    {
                        ownedEntity.CorpId = Context.CorpId;
                    }
                }
            }

            return entity;
        }
        /// <summary>
        /// 填充更新定义的系统字段（更新时间、更新人）。
        /// </summary>
        /// <param name="update">要填充的更新定义。</param>
        /// <returns>填充后的更新定义。</returns>
        protected override UpdateDefinition<T> FillSystemField(UpdateDefinition<T> update)
        {
            if (IEntityType.IsAssignableFrom(typeof(T)))
            {
                update = UpdateBuilder.Combine(UpdateBuilder.Set(Fields.UpdateBy, Context.Operator), UpdateBuilder.Set(Fields.UpdateTime, DateTime.UtcNow.ToTimeStampMs()));
            }
            return update;
        }
    }
}
