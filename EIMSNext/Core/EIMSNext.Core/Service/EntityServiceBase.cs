using EIMSNext.Common;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Entity;

using HKH.Mef2.Integration;

using MongoDB.Driver;

namespace EIMSNext.Core.Service
{
    public abstract class EntityServiceBase<T> : MongoEntityServiceBase<T>, IService<T> where T : class, IEntity
    {
        #region Variables

        #endregion 

        public EntityServiceBase(IResolver resolver)
            : base(resolver)
        {
        }

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

                if (ICorpOwnedType.IsAssignableFrom(typeof(T)))
                {
                    (entity as ICorpOwned)!.CorpId = Context.CorpId;
                }
            }

            return entity;
        }
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
