using System.Text.Json;
using EIMSNext.Component;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormNotifyService(IResolver resolver) : EntityServiceBase<FormNotify>(resolver), IFormNotifyService
    {
        protected override Task BeforeAdd(IEnumerable<FormNotify> entities, IClientSessionHandle? session)
        {
            var entity = entities.First();

            if (!string.IsNullOrEmpty(entity.DataFilter))
            {
                var condList = entity.DataFilter.DeserializeFromJson<ConditionList>();
                if (condList != null)
                {
                    entity.DataDynamicFilter = condList.ToDynamicFilter().SerializeToJson();
                }
                else
                {
                    entity.DataDynamicFilter = null;
                }
            }
            else
            {
                entity.DataDynamicFilter = null;
            }

            return Task.CompletedTask;
        }

        protected override Task BeforeReplace(FormNotify entity, IClientSessionHandle? session)
        {
            if (!string.IsNullOrEmpty(entity.DataFilter))
            {
                var condList = entity.DataFilter.DeserializeFromJson<ConditionList>();
                if (condList != null)
                {
                    entity.DataDynamicFilter = condList.ToDynamicFilter().SerializeToJson();
                }
                else
                {
                    entity.DataDynamicFilter = null;
                }
            }
            else
            {
                entity.DataDynamicFilter = null;
            }

            return Task.CompletedTask;
        }
    }
}
