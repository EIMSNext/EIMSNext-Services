using Asp.Versioning;
using EIMSNext.ApiClient.File;
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
        public async Task<IActionResult> Print(PrintRequest request)
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

            var fileClient = Resolver.Resolve<FileApiClient>();
            var uploadResult = await fileClient.UploadTemp(printResult.Content, printResult.FileName, IdentityContext.AccessToken);
            if (uploadResult == null || string.IsNullOrEmpty(uploadResult.DownloadUrl))
                return BadRequest("上传打印文件失败");

            return ApiResult.Success(new { downloadUrl = uploadResult.DownloadUrl, fileName = printResult.FileName }).ToActionResult();
        }
    }
}
