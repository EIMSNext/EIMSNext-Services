using System.Dynamic;

using EIMSNext.ApiCore;
using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Print;
using EIMSNext.Print.Abstractions;
using EIMSNext.File.Contracts;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Core.Nodes
{
    /// <summary>
    /// EventFlow 打印节点。
    /// </summary>
    public class EfPrintNode : EfNodeBase<EfPrintNode>
    {
        public EfPrintNode(IResolver resolver) : base(resolver)
        {
        }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            return ExecuteWithLog(context, dataContext =>
            {
                var setting = Metadata?.EfNodeSetting?.PrintSetting
                    ?? throw new InvalidOperationException("打印节点未配置");

                if (string.IsNullOrWhiteSpace(setting.SourceNodeId)
                    || string.IsNullOrWhiteSpace(setting.FormId)
                    || string.IsNullOrWhiteSpace(setting.PrintDefId))
                {
                    throw new InvalidOperationException("打印节点配置不完整");
                }

                if (!dataContext.NodeDatas.TryGetValue(setting.SourceNodeId, out var sourceNode)
                    || sourceNode.ActionDatas.Count == 0)
                {
                    throw new InvalidOperationException("打印对象没有可用数据");
                }

                if (!string.Equals(sourceNode.FormId, setting.FormId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("打印对象与打印模板所属表单不一致");
                }

                var formDef = GetFormDef(dataContext, setting.FormId);
                var printDef = Resolver.GetRepository<PrintDef>().Get(setting.PrintDefId);
                if (printDef == null
                    || printDef.DeleteFlag
                    || !string.Equals(printDef.CorpId, dataContext.CorpId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(printDef.FormId, setting.FormId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("打印模板不存在或不属于当前表单");
                }

                var formDatas = sourceNode.ActionDatas
                    .Select(x => x.FormData)
                    .ToList();
                var dataIds = formDatas.Select(x => x.Id).ToList();
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

                var datas = formDatas
                    .Select(data => (object)PrintDataFormatter.Format(
                        data,
                        formDef.Content.Items ?? [],
                        taskLogsByDataId.GetValueOrDefault(data.Id),
                        BuildPrintDataContext(data, tasksByDataId.GetValueOrDefault(data.Id), employeeNames, dataContext.WfStarter)))
                    .ToList();
                if (datas.Count == 0)
                {
                    throw new InvalidOperationException("打印对象没有可用数据");
                }

                using var printResult = new CustomPrintService().Print(
                    new PrintTemplate
                    {
                        Content = printDef.Content,
                        PrintType = (PrintType)(int)printDef.PrintType,
                    },
                    new PrintOption(),
                    datas);

                if (printResult.Content == null || printResult.Content == Stream.Null)
                {
                    throw new InvalidOperationException("打印服务未生成文件");
                }

                var fileName = NormalizeFileName(printResult.FileName);
                var fileSize = printResult.Content.CanSeek ? printResult.Content.Length / 1000 : 0;
                var attachment = Resolver.Resolve<IUploadedFileService>()
                    .Upload([new UploadedFileUpload(printResult.Content, fileName, fileSize)], dataContext.CorpId)
                    .Single();

                var attachmentData = new ExpandoObject();
                var attachmentValue = (IDictionary<string, object?>)attachmentData;
                attachmentValue["id"] = attachment.Id;
                attachmentValue["name"] = attachment.FileName;
                attachmentValue["fileName"] = attachment.FileName;
                attachmentValue["savePath"] = attachment.SavePath;
                attachmentValue["thumbPath"] = attachment.ThumbPath;
                attachmentValue["fileExt"] = attachment.FileExt;
                attachmentValue["fileSize"] = attachment.FileSize;
                attachmentValue["url"] = attachment.SavePath;
                attachmentValue["thumbUrl"] = attachment.ThumbPath;

                var outputData = new ExpandoObject();
                ((IDictionary<string, object?>)outputData)["printFile"] = attachmentData;
                var output = new FormData
                {
                    AppId = dataContext.AppId,
                    CorpId = dataContext.CorpId,
                    FormId = string.Empty,
                    Data = outputData,
                    CreateBy = dataContext.WfStarter,
                    CreateTime = DateTime.UtcNow.ToTimeStampMs(),
                };

                dataContext.NodeDatas[Metadata!.Id] = new EfNodeData
                {
                    NodeId = Metadata.Id,
                    SingleResult = true,
                    FormId = setting.FormId,
                    NodeExecResult = attachmentData,
                    ActionDatas = [new ActionFormData { State = DataState.Unchanged, FormData = output }],
                };

                return ExecutionResult.Next();
            }, "打印成功");
        }

        private static string NormalizeFileName(string? fileName)
        {
            var normalized = Path.GetFileName(fileName ?? string.Empty);
            return string.IsNullOrWhiteSpace(normalized) ? $"print_{DateTime.UtcNow:yyyyMMddHHmmssfff}" : normalized;
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

            var baseUrl = Resolver.GetAppSetting().WebHost.BaseUrl?.TrimEnd('/');
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
