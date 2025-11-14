using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class DepartmentService(IResolver resolver) : EntityServiceBase<Department>(resolver), IDepartmentService
    {
        protected override Task BeforeAdd(IEnumerable<Department> entities, IClientSessionHandle? session)
        {
            foreach (var entity in entities)
            {
                Repository.EnsureId(entity);

                if (!string.IsNullOrEmpty(entity.ParentId))
                {
                    var parent = Repository.Get(entity.ParentId);
                    if (parent == null)
                    {
                        entity.ParentId = "";
                        entity.HeriarchyId = $"|{entity.Id}|";
                        entity.HeriarchyName = entity.Name;
                    }
                    else
                    {
                        entity.HeriarchyId = $"{parent.HeriarchyId}{entity.Id}|";
                        entity.HeriarchyName = $"{entity.Name}/{parent.HeriarchyName}";
                    }
                }
                else
                {
                    entity.HeriarchyId = $"|{entity.Id}|";
                    entity.HeriarchyName = entity.Name;
                }
            }

            return base.BeforeAdd(entities, session);
        }
    }
}
