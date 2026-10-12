using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 实体服务基类，为包含审计字段的 <typeparamref name="T"/> 实体提供系统字段填充逻辑。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntity"/> 的实体类型。</typeparam>
    public abstract class EntityServiceBase<T> : EntityServiceBaseCore<T>, IService<T> where T : class, IEntity
    {
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

                // 逻辑删除：首次被标记删除时落删除人与删除时间（恢复时会清空，见 FormDataService）。
                if (entity.DeleteFlag && entity.DeleteTime is null)
                {
                    entity.DeleteBy = Context.Operator;
                    entity.DeleteTime = entity.UpdateTime;
                }
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
    }
}
