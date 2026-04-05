using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Print.Abstractions;
using EIMSNext.Service.Api.Requests;
using EIMSNext.Storage.Abstractions;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EIMSNext.Service.Api.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param> 
    [ApiVersion(1.0)]
    public class CustomPrintController(IResolver resolver) : MefControllerBase(resolver)
    {
        [HttpPost("Preview")]
        public async Task<IActionResult> Preview(PrintPreviewRequest request)
        {
            if (string.IsNullOrEmpty(request.Content))
                return ApiResult.Fail(400, "模板为空").ToActionResult();


            var printResult = new Print.CustomPrintService().Preview(new PrintTemplate { Content = request.Content, PrintType = (PrintType)(int)request.PrintType }, new PrintOption());

            if (printResult != null && !string.IsNullOrEmpty(printResult.FileName))
            {
                var savePath = $"{AppSetting.FileBasePath}\\Temp\\{IdentityContext.CurrentCorpId}\\{printResult.FileName}";
                var storage = Resolver.Resolve<IStorageProvider>();
                if (!storage.Upload(printResult.Content, savePath))
                    return ApiResult.Fail(500, "上传打印文件失败").ToActionResult();

                return ApiResult.Success(new { downloadUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{savePath.TrimStart('/', '\\').Replace("\\", "/")}", fileName = printResult.FileName }).ToActionResult();

            }
            else
                return ApiResult.Fail(500, "打印文件失败").ToActionResult();
        }

        [HttpPost("Print")]
        public async Task<IActionResult> Print(PrintRequest request)
        {
            if (string.IsNullOrEmpty(request.TemplateId) || request.DataIds == null || request.DataIds.Count == 0)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            var template = Resolver.Resolve<PrintTemplateApiService>().Get(request.TemplateId);
            if (template == null)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            //TODO: 考虑使用Find不查询UpdateLog
            var datas = Resolver.Resolve<FormDataApiService>().Query(x => request.DataIds.Contains(x.Id)).ToList();

            if (datas.Count == 0)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            var formDef = Resolver.Resolve<FormDefApiService>().Get(datas.First().FormId);

            if (formDef == null || formDef.Content.Items == null)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            var printResult = new Print.CustomPrintService().Print(new PrintTemplate { Content = template.Content, PrintType = (PrintType)(int)template.PrintType }, new PrintOption(), datas.Select(x => FormDataFormatter.Format(x, formDef.Content.Items)).ToList());

            if (printResult != null && !string.IsNullOrEmpty(printResult.FileName))
            {
                var savePath = $"{AppSetting.FileBasePath}\\Temp\\{IdentityContext.CurrentCorpId}\\{printResult.FileName}";
                var storage = Resolver.Resolve<IStorageProvider>();
                if (!storage.Upload(printResult.Content, savePath))
                    return ApiResult.Fail(500, "上传打印文件失败").ToActionResult();

                return ApiResult.Success(new { downloadUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{savePath.TrimStart('/', '\\').Replace("\\", "/")}", fileName = printResult.FileName }).ToActionResult();

            }
            else
                return ApiResult.Fail(500, "打印文件失败").ToActionResult();
        }
    }
}
