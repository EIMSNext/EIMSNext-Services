using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Print.Abstractions;
using EIMSNext.Service.Api.Requests;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param> 
    [ApiVersion(1.0)]
    public class CustomPrintController(IResolver resolver) : MefControllerBase(resolver)
    {
        [HttpPost]
        public IActionResult Print(PrintRequest request)
        {
            if (string.IsNullOrEmpty(request.TemplateId) || request.DataIds == null || request.DataIds.Count == 0)
                return BadRequest("数据或模板为空");

            var template = Resolver.Resolve<PrintTemplateApiService>().Get(request.TemplateId);
            if (template == null)
                return BadRequest("数据或模板为空");

            //TODO: 考虑使用Find不查询UpdateLog
            var datas = Resolver.Resolve<FormDataApiService>().Query(x => request.DataIds.Contains(x.Id)).ToList();

            if (datas.Count == 0)
                return BadRequest("数据或模板为空");

            var formDef = Resolver.Resolve<FormDefApiService>().Get(datas.First().FormId);

            if (formDef == null || formDef.Content.Items == null)
                return BadRequest("数据或模板为空");

            var printResult = new Print.CustomPrintService().Print(new PrintTemplate { Content = template.Content, PrintType = (PrintType)(int)template.PrintType }, new PrintOption(), datas.Select(x => FormDataFormatter.Format(x, formDef.Content.Items)).ToList());

            //TODO: Upload
            var downloadUrl = "";

            return ApiResult.Success(new { downloadUrl, printResult.FileName }).ToActionResult();
        }
    }
}
