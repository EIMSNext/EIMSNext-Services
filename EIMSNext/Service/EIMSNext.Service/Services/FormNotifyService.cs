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

            ParseDataFilter(entity);

            return Task.CompletedTask;
        }

        protected override Task BeforeReplace(FormNotify entity, IClientSessionHandle? session)
        {
            ParseDataFilter(entity);

            return Task.CompletedTask;
        }

        private void ParseDataFilter(FormNotify entity)
        {
            if (!string.IsNullOrEmpty(entity.DataFilter))
            {
                var condList = entity.DataFilter.DeserializeFromJson<ConditionList>();
                if (condList != null)
                {
                    if (entity.TriggerMode == FormNotifyTriggerMode.DataAdded || entity.TriggerMode == FormNotifyTriggerMode.DataChanged)
                    {
                        entity.DataDynamicFilter = null;
                        entity.DataExpressFilter = condList.ToScriptExpression();
                    }
                    else
                    {
                        entity.DataDynamicFilter = condList.ToDynamicFilter().SerializeToJson();
                        entity.DataExpressFilter = null;
                    }
                }
                else
                {
                    entity.DataDynamicFilter = null;
                    entity.DataExpressFilter = null;
                }
            }
            else
            {
                entity.DataDynamicFilter = null;
                entity.DataExpressFilter = null;
            }
        }
    }
}
