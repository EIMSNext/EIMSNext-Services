using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Common;
using EIMSNext.Core.Query;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.Authorization;
using EIMSNext.ServiceApi.Extension;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using MongoDB.Driver;

namespace EIMSNext.ServiceApi.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiController, ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class FormDataController(IResolver resolver) : MefControllerBase<FormData, FormData>(resolver)
    {
        /// <summary>
        /// 动态查询总数
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        [HttpPost]
        [Permission(Operation = Operation.Read)]
        [Route("$count")]
        public ActionResult GetCount([FromBody] DynamicFilter filter)
        {
            return Ok(ApiService.Count(filter));
        }
        /// <summary>
        /// 动态查询数据
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [HttpPost]
        [Permission(Operation = Operation.Read)]
        [Route("$query")]
        public ActionResult GetData([FromBody] DynamicFindOptions<FormData> options)
        {
            var result = ApiService.Find(options).ToList();
            return Ok(new { value = result.Cast(x => FormDataViewModel.FromFormData(x)) });
        }

        /// <summary>
        /// 单条查询
        /// </summary>
        /// <param name="key"></param>
        /// <param name="select"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{key}")]
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
        public async Task<IActionResult> Post(FormDataRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            FormData entity = model.CastTo<FormDataRequest, FormData>();
            //默认草稿
            entity.FlowStatus = EIMSNext.Core.FlowStatus.Draft;

            //if (!ValidateData(entity, null, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await (ApiService as FormDataApiService)!.AddAsync(entity, model.Action);
            return Ok(FormDataViewModel.FromFormData(entity));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("{key}")]
        public async Task<IActionResult> Put([FromRoute] string key, [FromBody] FormDataRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorString());
            }

            FormData entity = model.CastTo<FormDataRequest, FormData>();
            if (key != entity.Id)
            {
                return BadRequest("请求修改对象的Key不一致");
            }

            //if (!ValidateData(entity, null, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.ReplaceAsync(entity);
            return Ok(FormDataViewModel.FromFormData(entity));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        [HttpPatch]
        [Route("{key}")]
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

            FormDataRequest model = entity.CastTo<FormData, FormDataRequest>();

            delta.Patch(model);

            model.CopyTo(entity);

            //if (!ValidateData(entity, delta, out ApiResult? fail))
            //    return BadRequest(fail?.Message);

            await ApiService.ReplaceAsync(entity);

            return Ok(entity.CastTo<FormData, FormData>());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("{key}")]
        public async Task<IActionResult> Delete([FromRoute] string key)
        {
            await ApiService.DeleteAsync(key);

            return NoContent();
        }
    }
}
