using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.ApiHost.Extensions;
using EIMSNext.ApiService;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Print.Abstractions;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Requests;
using EIMSNext.Storage.Abstractions;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param> 
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
    public class CustomPrintController(IResolver resolver) : MefControllerBase(resolver)
    {
        [HttpPost("Preview")]
        public IActionResult Preview(PrintPreviewRequest request)
        {
            if (string.IsNullOrEmpty(request.Content))
                return ApiResult.Fail(400, "模板为空").ToActionResult();

            try
            {
                using var printResult = new Print.CustomPrintService().Preview(new PrintTemplate { Content = request.Content, PrintType = (PrintType)(int)request.PrintType }, new PrintOption());

                if (printResult != null && !string.IsNullOrEmpty(printResult.FileName))
                {
                    var savePath = $"{AppSetting.Storage.UploadFolder}\\Temp\\{IdentityContext.CurrentCorpId}\\{printResult.FileName}";
                    var storage = Resolver.Resolve<IStorageProvider>();
                    if (!storage.Upload(printResult.Content, savePath))
                        return ApiResult.Fail(500, "上传打印文件失败").ToActionResult();

                    return ApiResult.Success(new { downloadUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{savePath.TrimStart('/', '\\').Replace("\\", "/")}", fileName = printResult.FileName }).ToActionResult();

                }
                else
                    return ApiResult.Fail(500, "打印文件失败").ToActionResult();
            }
            catch (Exception ex)
            {
                return ApiResult.Fail(500, $"打印预览失败: {ex.Message}").ToActionResult();
            }
        }

        [HttpPost("Print")]
        public IActionResult Print(PrintRequest request)
        {
            if (string.IsNullOrEmpty(request.PrintId) || request.DataIds == null || request.DataIds.Count == 0)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            var template = Resolver.Resolve<PrintDefApiService>().Get(request.PrintId);
            if (template == null)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            //TODO: 考虑使用Find不查询UpdateLog
            var datas = Resolver.Resolve<FormDataApiService>().Query(x => request.DataIds.Contains(x.Id)).ToList();

            if (datas.Count == 0)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            var formDef = Resolver.Resolve<FormDefApiService>().Get(datas.First().FormId);

            if (formDef == null || formDef.Content.Items == null)
                return ApiResult.Fail(400, "数据或模板为空").ToActionResult();

            var dataIds = datas.Select(x => x.Id).ToList();
            var taskLogsByDataId = Resolver.GetRepository<Wf_TaskLog>()
                .Find(x => dataIds.Contains(x.DataId))
                .ToList()
                .GroupBy(x => x.DataId)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());
            var tasksByDataId = Resolver.GetRepository<Wf_Task>()
                .Find(x => dataIds.Contains(x.DataId))
                .ToList()
                .GroupBy(x => x.DataId)
                .ToDictionary(x => x.Key, x => x.ToList());
            var employeeIds = tasksByDataId.Values
                .SelectMany(x => x.Select(task => task.EmployeeId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            var employeeNames = Resolver.GetRepository<Employee>()
                .Find(x => employeeIds.Contains(x.Id))
                .ToList()
                .ToDictionary(x => x.Id, x => x.EmpName);
            var printedBy = IdentityContext.CurrentEmployee?.ToOperator();

            var printData = datas.Select(data => PrintDataFormatter.Format(
                data,
                formDef.Content.Items,
                taskLogsByDataId.GetValueOrDefault(data.Id),
                BuildPrintDataContext(data, tasksByDataId.GetValueOrDefault(data.Id), employeeNames, printedBy)))
                .Cast<object>()
                .ToList();

            using var printResult = new Print.CustomPrintService().Print(new PrintTemplate { Content = template.Content, PrintType = (PrintType)(int)template.PrintType }, new PrintOption(), printData);

            if (printResult != null && !string.IsNullOrEmpty(printResult.FileName))
            {
                    var savePath = $"{AppSetting.Storage.UploadFolder}\\Temp\\{IdentityContext.CurrentCorpId}\\{printResult.FileName}";
                var storage = Resolver.Resolve<IStorageProvider>();
                if (!storage.Upload(printResult.Content, savePath))
                    return ApiResult.Fail(500, "上传打印文件失败").ToActionResult();

                return ApiResult.Success(new { downloadUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{savePath.TrimStart('/', '\\').Replace("\\", "/")}", fileName = printResult.FileName }).ToActionResult();

            }
            else
                return ApiResult.Fail(500, "打印文件失败").ToActionResult();
        }

        private PrintDataContext BuildPrintDataContext(
            FormData data,
            IReadOnlyCollection<Wf_Task>? tasks,
            IReadOnlyDictionary<string, string> employeeNames,
            Operator? printedBy)
        {
            var currentTasks = tasks ?? [];
            var currentNode = string.Join("、", currentTasks
                .Select(x => x.ApproveNodeName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());
            var currentOwner = string.Join("、", currentTasks
                .Select(x => employeeNames.GetValueOrDefault(x.EmployeeId, string.Empty))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());

            var baseUrl = AppSetting.WebHost.BaseUrl?.TrimEnd('/');
            return new PrintDataContext
            {
                CurrentNode = currentNode,
                CurrentOwner = currentOwner,
                InternalDataUrl = string.IsNullOrWhiteSpace(baseUrl)
                    ? string.Empty
                    : $"{baseUrl}/#/app/{data.AppId}/form/{data.FormId}/data/{data.Id}",
                ExternalDataUrl = string.IsNullOrWhiteSpace(baseUrl)
                    ? string.Empty
                    : $"{baseUrl}/#/public/form/{data.FormId}/data/{data.Id}",
                PrintedBy = printedBy,
            };
        }
    }
}
