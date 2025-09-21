using HKH.Mef2.Integration;

using EIMSNext.Core;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;

using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormDefService(IResolver resolver) : EntityServiceBase<FormDef>(resolver), IFormDefService
    {
        protected override async Task AfterAdd(IEnumerable<FormDef> entities, IClientSessionHandle? session)
        {
            await base.AfterAdd(entities, session);
            var appRepo = Resolver.GetRepository<App>();
            var app = appRepo.Get(entities.First().AppId, session)!;
            var maxIndex = app.AppMenus.Count == 0 ? 0 : app.AppMenus.Max(x => x.SortIndex);
            entities.ForEach(e =>
            {
                maxIndex = maxIndex + 100;
                app.AppMenus.Add(new AppMenu { MenuId = e.Id, Icon = "", IconColor = "", MenuType = e.Type, Title = e.Name, SortIndex = maxIndex });
            });
            appRepo.Replace(app, session);

            return;
        }
    }
}
