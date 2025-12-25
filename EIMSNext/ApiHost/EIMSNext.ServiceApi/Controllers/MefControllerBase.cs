using System.Buffers;
using System.IO.Pipelines;
using System.Text;

using EIMSNext.ApiCore;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiService;
using EIMSNext.ApiService.Extension;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Entity;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="Q"></typeparam>
    public abstract class MefControllerBase<S, T, Q> : MefControllerBase
        where S : class, IApiService<T, Q>
        where T : class, IEntity
        where Q : T, new()
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        protected MefControllerBase(IResolver resolver) : base(resolver)
        {
            ApiService = resolver.GetApiService<S, T, Q>();
        }

        /// <summary>
        /// 服务接口
        /// </summary>
        protected S ApiService { get; private set; }
    }
}
