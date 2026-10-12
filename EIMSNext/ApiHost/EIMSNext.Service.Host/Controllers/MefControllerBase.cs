using System.Buffers;
using System.IO.Pipelines;
using System.Text;

using EIMSNext.ApiCore;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiService;
using EIMSNext.ApiService.Extensions;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Host.Authorization;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Q"></typeparam>
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
    public abstract class MefControllerBase<S, T> : MefControllerBase
        where S : class, IApiService<T>
        where T : class, IEntity
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        protected MefControllerBase(IResolver resolver) : base(resolver)
        {
            ApiService = resolver.GetApiService<S, T>();
        }

        /// <summary>
        /// 服务接口
        /// </summary>
        protected S ApiService { get; private set; }
    }
}

