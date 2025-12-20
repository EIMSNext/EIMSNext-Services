using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;
using System.Text;

using EIMSNext.ApiService;
using EIMSNext.ApiService.Extension;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.ServiceApi.Authorization;
using EIMSNext.ServiceApi.Extension;
using EIMSNext.ServiceApi.Request;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;

using MongoDB.AspNetCore.OData;

namespace EIMSNext.ServiceApi.OData
{
    /// <summary>
    /// OData的控制器基类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="V"></typeparam>
    [Authorize]
    public abstract class ReadOnlyODataController<S, T, V> : ODataController
        where S : class, IApiService<T, V>
        where T : class, IMongoEntity
        where V : class, T, new()
    {
        protected static readonly Type IDeleteFlagType = typeof(IDeleteFlag);
        protected static readonly Type ICorpOwnedType = typeof(ICorpOwned);

        /// <summary>
        /// 从ODATA内部类获取属性访问器
        /// </summary>
        protected static PropertyInfo InstanceAccesser = typeof(ODataController).Assembly.GetType("Microsoft.AspNetCore.OData.Query.Wrapper.SelectExpandWrapper")!.GetProperty("UntypedInstance")!;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver">对象容器</param>
        public ReadOnlyODataController(IResolver resolver)
        {
            this.Resolver = resolver;
            this.CacheClient = resolver.GetCacheClient();
            this.ApiService = resolver.GetApiService<T, V>();
            this.IdentityContext = resolver.GetIdentityContext();
        }

        /// <summary>
        /// 对象容器
        /// </summary>
        protected IResolver Resolver { get; private set; }

        /// <summary>
        /// 缓存
        /// </summary>
        protected ICacheClient CacheClient { get; private set; }

        /// <summary>
        /// 服务接口
        /// </summary>
        protected IApiService<T, V> ApiService { get; private set; }
        /// <summary>
        /// 当前用户上下文
        /// </summary>
        protected IIdentityContext IdentityContext { get; private set; }

        /// <summary>
        /// 是否可以访问
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected virtual bool IsOwnerAllowed(T entity)
        {
            return false;
            //return !(IdentityContext.AccessControlLevel == AccessControlLevel.Owner && (IdentityContext.IdentityType == IdentityType.Terminal || IdentityContext.IdentityType == IdentityType.NonRegister) &&
            //   IdentityContext.CurrentUserID != entity.CreateBy.Id);
        }

        /// <summary>
        /// OData查询
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Permission(Operation = Operation.Read)]
        [MongoEnableQuery]
        public virtual IActionResult Get(ODataQueryOptions<V> options)
        {
            var query = ApiService.All();

            query = FilterResult(query);
            query = Expand(query, options);

            return Ok(query);
        }

        /// <summary>
        /// 添加扩展实现
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual IQueryable<V> Expand(IQueryable<V> query, ODataQueryOptions<V> options)
        {
            return query;
        }
        /// <summary>
        /// 对按请求的上下文进行数据过滤，比如用户只能访问被授权的数据
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected virtual IQueryable<V> FilterResult(IQueryable<V> query)
        {
            return FilterByPermission(FilterByDeleted(FilterByCorpId(query)));
        }
        protected IQueryable<V> FilterByDeleted(IQueryable<V> query)
        {
            if (IDeleteFlagType.IsAssignableFrom(typeof(V)))
                return query.Where(x => !((x as IDeleteFlag)!.DeleteFlag ?? false));
            else
                return query;
        }
        protected IQueryable<V> FilterByCorpId(IQueryable<V> query)
        {
            if (ICorpOwnedType.IsAssignableFrom(typeof(V)))
                return query.Where(x => (x as ICorpOwned)!.CorpId == IdentityContext.CurrentCorpId);
            else
                return query;
        }
        protected virtual IQueryable<V> FilterByPermission(IQueryable<V> query)
        {
            return query;
        }

        /// <summary>
        /// OData查询
        /// </summary>
        /// <param name="key">主键Id</param>
        /// <param name="options"></param>
        /// <returns></returns>
        [HttpGet]
        [Permission(Operation = Operation.Read)]
        [MongoEnableQuery]
        public virtual SingleResult Get([FromODataUri] string key, ODataQueryOptions<V> options)
        {
            var query = ApiService.Query(x => x.Id == key);

            query = Expand(query, options);

            return SingleResult.Create(query);
        }

        /// <summary>
        /// 获取被selectexpandwrapper包装的对象
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected T GetInstance(object obj)
        {
            if (obj is T) { return (T)obj; }
            return (T)InstanceAccesser.GetValue(obj)!;
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //protected string CorpFilter
        //{
        //    get
        //    {
        //        var orgId = Request.Query["orgfilter"].FirstOrDefault();
        //        return string.IsNullOrEmpty(orgId) ? 0 : orgId.SafeToInt64();
        //    }
        //}
    }

    /// <summary>
    /// OData的控制器基类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="V"></typeparam>
    /// <typeparam name="R"></typeparam>
    [Authorize]
    public abstract class ODataController<S, T, V, R> : ReadOnlyODataController<S, T, V>
        where S : class, IApiService<T, V>
        where T : class, IEntity
        where V : class, T, new()
        where R : class, IMongoEntity
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="resolver">对象容器</param>
        public ODataController(IResolver resolver)
           : base(resolver)
        { }

        /// <summary>
        /// 新增
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> Post([FromBody] R model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            T entity = model.CastTo<R, T>();

            if (!ValidateData(entity, null, out ApiResult? fail))
                return BadRequest(fail?.Message);

            await ApiService.AddAsync(entity);
            return Ok(entity.CastTo<T, V>());
        }

        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="key">主键Id</param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> Put([FromODataUri] string key, [FromBody] R model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            if (key != model.Id)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            T? entity = await ApiService.GetAsync(key);
            if (entity == null) return NotFound();

            model.CopyTo(entity);

            await ApiService.ReplaceAsync(entity);
            return Ok(entity.CastTo<T, V>());
        }

        /// <summary>
        /// 部分字段修改
        /// </summary>
        /// <param name="key">主键Id</param>
        /// <param name="delta"></param>
        /// <returns></returns>
        [HttpPatch]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<R> delta)
        {
            if (delta == null)
            {
                return BadRequest("数据解析失败，请检查数据格式, 确认正确的字段名和数据类型");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }
            else if (TryGetId(delta, out string sId) && key != sId)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            T? entity = await ApiService.GetAsync(key);
            if (entity == null) return NotFound();

            R model = entity.CastTo<T, R>();

            delta.Patch(model);

            model.CopyTo(entity);

            if (!ValidateData(entity, delta, out ApiResult? fail))
                return BadRequest(fail?.Message);

            await ApiService.ReplaceAsync(entity);

            return Ok(entity.CastTo<T, V>());
        }

        /// <summary>
        /// 批量部分字段修改
        /// </summary>
        /// <param name="deltas"></param>
        /// <returns></returns>
        [HttpPatch]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> Patch([FromODataUri] DeltaSet<R> deltas)
        {
            if (deltas == null)
                return BadRequest("数据解析失败，请检查数据格式, 确认正确的字段名和数据类型");

            var success = new List<T>();
            var failed = new List<ApiResult>();
            foreach (Delta<R> delta in deltas)
            {
                if (TryGetId(delta, out string sId))
                {
                    T? entity = await ApiService.GetAsync(sId);
                    if (entity != null)
                    {
                        R model = entity.CastTo<T, R>();

                        delta.Patch(model);

                        model.CopyTo(entity);

                        if (!ValidateData(entity, delta, out ApiResult? fail))
                        {
                            failed.Add(fail!);
                        }
                        else
                        {
                            await ApiService.ReplaceAsync(entity);
                            success.Add(entity);
                        }
                    }
                    else
                    {
                        failed.Add(ApiResult.Fail(-1, "对象不存在", delta.GetInstance()));
                    }
                }
            }

            return Ok(new { success, failed });
        }

        /// <summary>
        /// 获取Delta对象中的Id值
        /// </summary>
        /// <param name="delta"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        protected bool TryGetId(Delta<R> delta, out string id)
        {
            id = string.Empty;
            object _id;
            if (delta.TryGetPropertyValue("Id", out _id) && _id != null)
                id = _id.ToString()!;
            else if (delta.TryGetPropertyValue("_id", out _id) && _id != null)
                id = _id.ToString()!;

            return !string.IsNullOrEmpty(id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="model"></param>
        /// <param name="failResult"></param>
        /// <returns></returns>
        protected virtual bool ValidateData(T entity, Delta<R>? model, out ApiResult? failResult)
        {
            failResult = null;
            return true;
        }

        /// <summary>
        /// 删除，支持两种方式 1.请求地址中key != batch, 则删除指定对象 2.请求地址中key == batch, 请求体中keys = 1,2,3...可以批量删除
        /// </summary>
        /// <param name="key">主键Id</param>
        /// <param name="batch">批量删除</param>
        /// <returns></returns>
        [HttpDelete]
        [Permission(Operation = Operation.Write)]
        public virtual async Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            if ("batch".EqualsIgnoreCase(key))
            {
                if (batch?.Keys?.Count > 0)
                {
                    await ApiService.DeleteAsync(batch.Keys);
                }
                else
                {
                    return BadRequest();
                }
            }
            else
            {
                await ApiService.DeleteAsync(key);
            }
            return NoContent();
        }

        /// <summary>
        /// 获取请求的数据
        /// </summary>
        protected async Task<string> GetBodyAsync()
        {
            var bodyReader = Request.BodyReader;
            ReadResult readResult;
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
    }

    ///// <summary>
    ///// OData的控制器基类
    ///// </summary>
    ///// <typeparam name="T"></typeparam>
    //[Authorize]
    //public abstract class ExtendODataController<T> : ODataController<T> where T : class, IEntity
    //{
    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <param name="cache"></param>
    //    /// <param name="service"></param>
    //    /// <param name="attachmentService"></param>
    //    /// <param name="identityContext"></param>
    //    protected ExtendODataController(IMemoryCache cache, IBusinessService<T> service, ISysAttachmentService attachmentService, IIdentityContext identityContext) : base(cache, service, identityContext)
    //    {
    //        this.SysAttachmentService = attachmentService;
    //    }

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    protected ISysAttachmentService SysAttachmentService { get; private set; }

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <param name="options"></param>
    //    /// <returns></returns>
    //    public override ActionResult<IQueryable<T>> Get(ODataQueryOptions<T> options)
    //    {
    //        var inlinecount = Request.Query["$inlinecount"].FirstOrDefault();
    //        var apiResult = options.ApplyTo<T>(Service.FilterByUser(IdentityContext.CurrentUserID), "allpages" == inlinecount);
    //        if (RequestExtend && (options.SelectExpand == null || options.SelectExpand.RawSelect == null))
    //        {
    //            foreach (dynamic item in apiResult.Data!.Results)
    //            {
    //                Extend(GetInstance(item), options);
    //            }
    //        }
    //        return Ok(apiResult);
    //    }

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <param name="key"></param>
    //    /// <returns></returns>
    //    public override ActionResult<T> Get([FromRoute] long key)
    //    {
    //        T? entity = Service.Get(key);
    //        if (entity == null) return NotFound();

    //        if (RequestExtend) Extend(entity, null);

    //        return Ok(ApiResult.Success(entity));
    //    }

    //    /// <summary>
    //    /// 对已有对象的扩展属性进行赋值
    //    /// </summary>
    //    /// <param name="entity"></param>
    //    /// <param name="options"></param>
    //    protected virtual void Extend(T entity, ODataQueryOptions<T>? options)
    //    { }

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    /// <returns></returns>
    //    protected bool RequestExtend => (Request.Query["extend"].FirstOrDefault() ?? "0") == "1";
    //}
}
