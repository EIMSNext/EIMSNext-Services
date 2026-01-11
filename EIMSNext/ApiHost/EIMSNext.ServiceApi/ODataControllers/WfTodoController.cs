using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.ServiceApi.OData;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core;
using EIMSNext.Entity;

using Microsoft.AspNetCore.OData.Query;
using EIMSNext.ApiService;

namespace EIMSNext.ServiceApi.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class WfTodoController(IResolver resolver) : ODataController<WfTodoApiService, Wf_Todo, WfTodoViewModel, WfTodoRequest>(resolver)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected override IQueryable<WfTodoViewModel> Expand(IQueryable<WfTodoViewModel> query, ODataQueryOptions<WfTodoViewModel> options)
        {
            var formDefs = Resolver.GetService<FormDef>().All();
            var formDataDefs = Resolver.GetService<FormData>().All();
            query = query.Join(formDefs, x => x.FormId, y => y.Id,
                   //ObjectConvert.ProjExp<WfTodoViewModel, FormDef, string>(x => x.FormName, y => y.Name)               
                   (x, y) =>
                      new WfTodoViewModel
                      {
                          Id = x.Id,
                          WfInstanceId = x.WfInstanceId,
                          CorpId = x.CorpId,
                          AppId = x.AppId,
                          FormId = x.FormId,
                          DataId = x.DataId,
                          EmployeeId = x.EmployeeId,
                          ApproveNodeId = x.ApproveNodeId,
                          ApproveNodeName = x.ApproveNodeName,
                          FormType = x.FormType,
                          CreateBy = x.CreateBy,
                          CreateTime = x.CreateTime,
                          UpdateBy = x.UpdateBy,
                          UpdateTime = x.UpdateTime,
                          FormName = y.Name,
                          Starter = x.Starter,
                          DataBrief = x.DataBrief,
                          ApproveNodeStartTime = x.ApproveNodeStartTime,
                      }

                   );

            return base.Expand(query, options);
        }

        protected override IQueryable<WfTodoViewModel> FilterByPermission(IQueryable<WfTodoViewModel> query, ODataQueryOptions<WfTodoViewModel> options)
        {
            if (IdentityContext.CurrentEmployee != null)
            {
                var empId = IdentityContext.CurrentEmployee.Id;
                return query.Where(x => x.EmployeeId == empId);
            }

            return query;
        }
    }
}
