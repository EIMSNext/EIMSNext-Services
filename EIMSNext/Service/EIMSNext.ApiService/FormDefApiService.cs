﻿using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModel;
using EIMSNext.Component;
using EIMSNext.Entity;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class FormDefApiService(IResolver resolver) : ApiServiceBase<FormDef, FormDefViewModel>(resolver)
	{
        public override Task AddAsync(FormDef entity)
        {
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            return base.AddAsync(entity);
        }

        public override Task<ReplaceOneResult> ReplaceAsync(FormDef entity)
        {
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            return base.ReplaceAsync(entity);
        }
    }
}
