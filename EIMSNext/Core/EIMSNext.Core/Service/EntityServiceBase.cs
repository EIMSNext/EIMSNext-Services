using HKH.Mef2.Integration;
using EIMSNext.Core.Entity;
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
            if (IEntityType.IsAssignableFrom(typeof(T)))
            {
                if (isEdit)
                {
                    entity.UpdateBy = Context.Operator;
                    entity.UpdateTime = DateTime.UtcNow;
                }
                else
                {
                    entity.CreateBy = Context.Operator;
                    entity.UpdateBy = entity.CreateBy;
                    entity.CreateTime = DateTime.UtcNow;
                    entity.UpdateTime = entity.CreateTime;
                }
            }
            return entity;
        }
        protected override UpdateDefinition<T> FillSystemField(UpdateDefinition<T> update)
        {
            if (IEntityType.IsAssignableFrom(typeof(T)))
            {
                update = UpdateBuilder.Combine(UpdateBuilder.Set(Common.Constants.Field_UpdateBy, Context.Operator), UpdateBuilder.Set(Common.Constants.Field_UpdateTime, DateTime.UtcNow));
            }
            return update;
        }
    }
}
