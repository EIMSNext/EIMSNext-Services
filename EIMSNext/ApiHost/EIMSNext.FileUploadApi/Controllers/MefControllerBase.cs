using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using EIMSNext.ApiCore;
using EIMSNext.Common;
using EIMSNext.FileUploadApi.Authorization;
using EIMSNext.FileUploadApi.Extension;
using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.FileUploadApi.Controllers
{
    public abstract class MefControllerBase : ControllerBase
    {
        /// <summary>
        /// 对象容器
        /// </summary>
        protected IResolver Resolver { get; private set; }
        /// <summary>
        /// MEF对象容器
        /// </summary>
        //protected CompositionContainer MefContainer { get; private set; }
        /// <summary>
        /// 缓存
        /// </summary>
        //protected ICacheClient Cache { get; private set; }
        /// <summary>
        /// 当前用户上下文
        /// </summary>
        protected IIdentityContext IdentityContext { get; private set; }
        /// <summary>
        /// 系统配置
        /// </summary>
        protected AppSetting AppSetting { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver"></param>
        public MefControllerBase(IResolver resolver)
        {
            Resolver = resolver;
            //MefContainer = resolver.MefContainer;
            //Cache = resolver.GetCacheClient();
            IdentityContext = resolver.GetIdentityContext();
            AppSetting = resolver.GetAppSetting();
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

        protected IActionResult Error(int errCode, string errMsg, dynamic? data = null)
        {
            return new ObjectResult(new { code = errCode, message = errMsg, data })
            {               
                StatusCode = errCode
            };
        }
    }
}
