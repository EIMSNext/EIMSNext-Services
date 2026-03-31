using System.Text.Json;
using Asp.Versioning;

using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Api.Authorization;
using EIMSNext.Service.Api.Requests;

using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;

using MongoDB.Driver;

namespace EIMSNext.Service.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class FormDataController(IResolver resolver) : MefControllerBase<FormDataApiService, FormData, FormData>(resolver)
    {
        /// <summary>
        /// 动态查询总数
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpPost("dynamic/$count")]
        public ActionResult GetDynamicCount([FromBody] DynamicFilter filter)
        {
            //TODO: fill field type
            return Ok(ApiService.Count(filter));
        }
        /// <summary>
        /// 动态查询数据
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpPost("dynamic/$query")]
        public ActionResult GetDynamicData([FromBody] DynamicFindOptions<FormData> options)
        {
            //TODO: fill field type
            var result = ApiService.Find(FilterResult(options)).ToList();
            return Ok(new { value = result.Cast(x => FormDataViewModel.FromFormData(x)) });
        }

        /// <summary>
        /// 动态查询总数
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpPost("$count")]
        public ActionResult GetCount([FromBody] DynamicFilter filter)
        {
            return Ok(ApiService.Count(filter));
        }
        /// <summary>
        /// 动态查询数据
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpPost("$query")]
        public ActionResult GetData([FromBody] DynamicFindOptions<FormData> options)
        {
            if (options.Select == null || options.Select.Count == 0)
            {
                //不指定列时，不返回UpdateLog
                options.Select = new DynamicFieldList()
                {
                    DynamicField.Create("updateLog",false)
                };
            }
            var result = ApiService.Find(FilterResult(options)).ToList();
            return Ok(new { value = result.Cast(x => FormDataViewModel.FromFormData(x)) });
        }

        /// <summary>
        /// 对按请求的上下文进行数据过滤，比如用户只能访问被授权的数据
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected virtual DynamicFindOptions<FormData> FilterResult(DynamicFindOptions<FormData> query)
        {
            return FilterByPermission(FilterByDeleted(FilterByCorpId(query)));
        }
        protected DynamicFindOptions<FormData> FilterByDeleted(DynamicFindOptions<FormData> query)
        {
            var filter = query.Filter;
            if (filter == null) { filter = new DynamicFilter(); }
            if (filter.IsGroup && filter.Rel == FilterRel.And)
            {
                filter.Items!.Add(new DynamicFilter() { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true });
            }
            else
            {
                filter = new DynamicFilter() { Rel = FilterRel.And, Items = [new DynamicFilter() { Field = Fields.DeleteFlag, Op = FilterOp.Ne, Value = true }, filter] };
            }

            query.Filter = filter;
            return query;
        }
        protected DynamicFindOptions<FormData> FilterByCorpId(DynamicFindOptions<FormData> query)
        {
            var filter = query.Filter;
            if (filter == null) { filter = new DynamicFilter(); }
            if (filter.IsGroup && filter.Rel == FilterRel.And)
            {
                filter.Items!.Add(new DynamicFilter() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = IdentityContext.CurrentCorpId });
            }
            else
            {
                filter = new DynamicFilter() { Rel = FilterRel.And, Items = [new DynamicFilter() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = IdentityContext.CurrentCorpId }, filter] };
            }

            query.Filter = filter;
            return query;
        }
        protected virtual DynamicFindOptions<FormData> FilterByPermission(DynamicFindOptions<FormData> query)
        {
            if (query.Scope != null)
            {
                if (!string.IsNullOrEmpty(query.Scope.AuthGroupId))
                {
                    var authGrp = Resolver.GetService<AuthGroup>().Get(query.Scope.AuthGroupId);
                    if (authGrp != null)
                    {
                        var filter = query.Filter;
                        if (filter == null) { filter = new DynamicFilter(); }

                        if (authGrp.Type == AuthGroupType.ManageSelfData)
                        {
                            if (filter.IsGroup && filter.Rel == FilterRel.And)
                            {
                                filter.Items!.Add(new DynamicFilter() { Field = $"{Fields.CreateBy}.empId", Op = FilterOp.Eq, Value = IdentityContext.CurrentEmployee!.Id });
                            }
                            else
                            {
                                filter = new DynamicFilter() { Rel = FilterRel.And, Items = [new DynamicFilter() { Field = $"{Fields.CreateBy}.empId", Op = FilterOp.Eq, Value = IdentityContext.CurrentEmployee!.Id }, filter] };
                            }
                        }
                        else if (authGrp.Type == AuthGroupType.Custom)
                        {
                            if (!string.IsNullOrEmpty(authGrp.DataFilter))
                            {
                                var condList = authGrp.DataFilter.DeserializeFromJson<ConditionList>();
                                if (condList != null)
                                {
                                    var dataFilter = condList.ToDynamicFilter();

                                    if (filter.IsGroup && filter.Rel == FilterRel.And)
                                    {
                                        filter.Items!.Add(dataFilter);
                                    }
                                    else
                                    {
                                        filter = new DynamicFilter() { Rel = FilterRel.And, Items = [dataFilter, filter] };
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return query;
        }

        /// <summary>
        /// 单条查询
        /// </summary>
        /// <param name="key"></param>
        /// <param name="select"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Read)]
        [HttpGet("{key}")]
        public ActionResult Get([FromRoute] string key, [FromQuery] string? select)
        {
            var fields = new DynamicFieldList();
            if (!string.IsNullOrEmpty(select))
            {
                foreach (var field in select.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    fields.Add(new DynamicField { Field = field, Visible = true });
                }
            }

            var options = new DynamicFindOptions<FormData>() { Select = fields, Filter = new DynamicFilter { Field = "_id", Op = FilterOp.Eq, Value = key } };
            var result = ApiService.Find(options).FirstOrDefault();
            if (result == null)
            {
                return NotFound();
            }

            return Ok(FormDataViewModel.FromFormData(result));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Permission(Operation = Operation.Write)]
        public async Task<IActionResult> Post([FromBody] FormDataRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            FormData entity = model.CastTo<FormDataRequest, FormData>();
            //默认草稿
            entity.FlowStatus = FlowStatus.Draft;

            //if (!ValidateData(entity, null, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.AddAsync(entity, model.Action);
            return Ok(FormDataViewModel.FromFormData(entity));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Write)]
        [HttpPut("{key}")]
        public async Task<IActionResult> Put([FromRoute] string key, [FromBody] FormDataRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }
            //根据key获取数据库中的实体
            FormData? entity = ApiService.Get(key);
            if (entity == null) return NotFound();

            ServiceContext.SessionStore.Set(entity.Id, entity.DeepClone(), DataVersion.Old);

            //保存原始实体的重要字段
            var originalCorpId = entity.CorpId;
            var originalDeleteFlag = entity.DeleteFlag;

            //将请求的数据直接复制到原始实体，而不是通过中间转换
            model.CopyTo(entity);

            //恢复重要字段，确保不会丢失
            entity.CorpId = originalCorpId;
            entity.DeleteFlag = originalDeleteFlag;


            if (key != entity.Id)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            //if (!ValidateData(entity, null, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.ReplaceAsync(entity, model.Action);
            return Ok(FormDataViewModel.FromFormData(entity));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        [Permission(Operation = Operation.Write)]
        [HttpPatch("{key}")]
        public async Task<IActionResult> Patch([FromRoute] string key, [FromBody] Delta<FormDataRequest> delta)
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

            FormData? entity = ApiService.Get(key);
            if (entity == null) return NotFound();

            ServiceContext.SessionStore.Set(entity.Id, entity.DeepClone(), DataVersion.Old);

            FormDataRequest model = entity.CastTo<FormData, FormDataRequest>();

            delta.Patch(model);

            model.CopyTo(entity);

            //if (!ValidateData(entity, delta, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.ReplaceAsync(entity);

            return Ok(entity.CastTo<FormData, FormData>());
        }

        /// <summary>
        /// 删除，支持两种方式 1.请求地址中key != batch, 则删除指定对象 2.请求地址中key == batch, 请求体中keys = 1,2,3...可以批量删除
        /// </summary>
        /// <param name="key">主键Id</param>
        /// <param name="batch">批量删除</param>
        /// <returns></returns>
        [Permission(Operation = Operation.Write)]
        [HttpDelete("{key}")]
        public async Task<ActionResult> Delete([FromRoute] string key, [FromBody] DeleteBatch? batch)
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
    }
}
