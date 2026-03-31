using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Component;
using EIMSNext.Service.Entities;

using MongoDB.Driver;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
    public class FormDefApiService(IResolver resolver) : ApiServiceBase<FormDef, FormDefViewModel, IFormDefService>(resolver)
	{
        public override Task AddAsync(FormDef entity)
        {
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            return base.AddAsync(entity);
        }

        public override Task<ReplaceOneResult> ReplaceAsync(FormDef entity)
        {
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            ServiceContext.SessionStore.Set(entity.Id, entity, Cache.DataVersion.New);

            return base.ReplaceAsync(entity);
        }
    }
}
