using Asp.Versioning;

using HKH.Mef2.Integration;

using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;

using Microsoft.AspNetCore.OData.Query;
using EIMSNext.ApiService;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
        public class WfTaskController(IResolver resolver) : ODataController<WfTaskApiService, Wf_Task, WfTaskViewModel, WfTaskRequest>(resolver)
    {
        protected override IQueryable<WfTaskViewModel> FilterResult(IQueryable<WfTaskViewModel> query, ODataQueryOptions<WfTaskViewModel> options)
        {
            return FilterByPermission(query, options);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected override IQueryable<WfTaskViewModel> Expand(IQueryable<WfTaskViewModel> query, ODataQueryOptions<WfTaskViewModel> options)
        {
            var formDefs = Resolver.GetService<FormDef>().All();
            query = query.Join(formDefs, x => x.FormId, y => y.Id,
                   //ObjectConvert.ProjExp<WfTaskViewModel, FormDef, string>(x => x.FormName, y => y.Name)
                   (x, y) =>
                      new WfTaskViewModel
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
                          ExpireTime = x.ExpireTime,
                          ExpireHandled = x.ExpireHandled,
                        }

                   );

            return base.Expand(query, options);
        }

        protected override IQueryable<WfTaskViewModel> FilterByPermission(IQueryable<WfTaskViewModel> query, ODataQueryOptions<WfTaskViewModel> options)
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
