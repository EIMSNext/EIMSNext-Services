using System.Buffers;
using System.IO.Pipelines;
using System.Text;

using EIMSNext.ApiCore;
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

namespace EIMSNext.ApiHost.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [ApiController, Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    public abstract class MefControllerBase : ControllerBase
    {
        /// <summary>
        /// 对象容器
        /// </summary>
        protected IResolver Resolver { get; private set; }
        /// <summary>
        /// MEF对象容器
        /// </summary>
        protected CompositionContainer MefContainer { get; private set; }
        /// <summary>
        /// 缓存
        /// </summary>
        protected ICacheClient Cache { get; private set; }
        /// <summary>
        /// 当前用户上下文
        /// </summary>
        protected IIdentityContext IdentityContext { get; private set; }
        /// <summary>
        /// 系统配置
        /// </summary>
        protected AppSetting AppSetting { get; private set; }

        protected IServiceContext ServiceContext { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        public MefControllerBase(IResolver resolver)
        {
            this.Resolver = resolver;
            MefContainer = resolver.MefContainer;
            this.Cache = resolver.GetCacheClient();
            this.IdentityContext = resolver.GetIdentityContext();
            this.AppSetting = resolver.GetAppSetting();
            ServiceContext = resolver.GetServiceContext();
        }

        /// <summary>
        /// 获取请求的数据
        /// </summary>
        private async Task<string> GetBodyAsync()
        {
            var bodyReader = Request.BodyReader;
            ReadResult readResult = new ReadResult();
            while (true)
            {
                readResult = await bodyReader.ReadAsync();
                if (readResult.IsCompleted) { break; }
                bodyReader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
            }
            var buffer = readResult.Buffer;
            bodyReader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.Start);

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        //private JsonObject? GetAccessToken(string userName)
        //{
        //    RestClient restClient = new RestClient(AppSetting.OAuth_TokenEndPoint!);
        //    var request = new RestRequest("", Method.Post);
        //    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        //    request.AddParameter("application/x-www-form-urlencoded", $@"grant_type=password&client_id=1&username={userName}&password={UrlEncoder.Default.Encode("(!@#^&*$%) [,./';:>?<]")}", ParameterType.RequestBody);

        //    return restClient.Execute<JsonObject>(request).Data;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errCode"></param>
        /// <param name="errMsg"></param>
        /// <returns></returns>
        protected IActionResult Error(int errCode, string errMsg)
        {
            return new ODataErrorResult(errCode.ToString(), errMsg);
        }

        /// <summary>
        /// 获取Delta对象中的Id值
        /// </summary>
        /// <param name="delta"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        protected bool TryGetId<R>(Delta<R> delta, out string id) where R : class
        {
            id = string.Empty;
            object _id;
            if (delta.TryGetPropertyValue("Id", out _id) && _id != null)
                id = _id.ToString()!;
            else if (delta.TryGetPropertyValue("_id", out _id) && _id != null)
                id = _id.ToString()!;

            return !string.IsNullOrEmpty(id);
        }
    }
}
