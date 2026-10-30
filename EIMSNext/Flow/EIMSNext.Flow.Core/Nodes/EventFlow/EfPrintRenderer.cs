using EIMSNext.ApiCore;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Entities;
using EIMSNext.Print;
using EIMSNext.Print.Abstractions;

using HKH.Mef2.Integration;

namespace EIMSNext.Flow.Core.Nodes
{
    /// <summary>
    /// EventFlow 打印产物渲染器：打印节点执行与消费点产物失效重渲染共用。
    /// 渲染是无状态纯函数（源数据快照 + 打印模板 → 文件），重复渲染无副作用，
    /// 产物仅在下游首次消费时上传（<see cref="EfNodeBase{T}.MaterializePrintOutput"/>）。
    /// </summary>
    public class EfPrintRenderer
    {
        private readonly IResolver resolver;

        public EfPrintRenderer(IResolver resolver)
        {
            this.resolver = resolver;
        }

        public EfPendingPrintFile Render(EfDataContext dataContext, string sourceNodeId, string formId, string printDefId)
        {
            if (!dataContext.NodeDatas.TryGetValue(sourceNodeId, out var sourceNode)
                || sourceNode.ActionDatas.Count == 0)
            {
                throw new InvalidOperationException("打印对象没有可用数据");
            }

            if (!string.Equals(sourceNode.FormId, formId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("打印对象与打印模板所属表单不一致");
            }

            var formDef = GetFormDef(dataContext, formId);
            var printDef = resolver.GetRepository<PrintDef>().Get(printDefId);
            if (printDef == null
                || printDef.DeleteFlag
                || !string.Equals(printDef.CorpId, dataContext.CorpId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(printDef.FormId, formId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("打印模板不存在或不属于当前表单");
            }

            var formDatas = sourceNode.ActionDatas
                .Select(x => x.FormData)
                .ToList();
            var dataIds = formDatas.Select(x => x.Id).ToList();
            var taskLogsByDataId = resolver.GetRepository<Wf_TaskLog>()
                .Find(x => x.CorpId == dataContext.CorpId
                    && x.FormId == formId
                    && dataIds.Contains(x.DataId))
                .ToList()
                .GroupBy(x => x.DataId)
                .ToDictionary(x => x.Key, x => x.AsEnumerable());
            var tasksByDataId = resolver.GetRepository<Wf_Task>()
                .Find(x => dataIds.Contains(x.DataId))
                .ToList()
                .GroupBy(x => x.DataId)
                .ToDictionary(x => x.Key, x => x.ToList());
            var employeeIds = tasksByDataId.Values
                .SelectMany(x => x.Select(task => task.EmployeeId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            var employeeNames = resolver.GetRepository<Employee>()
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

            var fileName = Path.GetFileName(printResult.FileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"print_{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            }

            var fileSize = printResult.Content.CanSeek ? printResult.Content.Length / 1000 : 0;
            using var content = new MemoryStream();
            printResult.Content.CopyTo(content);

            return new EfPendingPrintFile
            {
                FileName = fileName,
                Content = content.ToArray(),
                FileSize = fileSize,
            };
        }

        /// <summary>
        /// run 跨进程恢复（重试/暂停）后内存产物丢失：用 marker 中的持久化要素重新渲染。
        /// 旧版 marker 缺要素时 fail-closed，避免内部标记落库。
        /// </summary>
        public EfPendingPrintFile RenderFromMarker(EfDataContext dataContext, IReadOnlyDictionary<string, object?> marker)
        {
            var sourceNodeId = marker.GetValueOrDefault(EfPendingPrintFile.SourceNodeIdKey) as string;
            var formId = marker.GetValueOrDefault(EfPendingPrintFile.FormIdKey) as string;
            var printDefId = marker.GetValueOrDefault(EfPendingPrintFile.PrintDefIdKey) as string;
            if (string.IsNullOrWhiteSpace(sourceNodeId)
                || string.IsNullOrWhiteSpace(formId)
                || string.IsNullOrWhiteSpace(printDefId))
            {
                throw new InvalidOperationException("打印产物已失效，请从打印节点重新执行");
            }

            return Render(dataContext, sourceNodeId, formId, printDefId);
        }

        private FormDef GetFormDef(EfDataContext dataContext, string formId)
        {
            if (!dataContext.FormDefs.TryGetValue(formId, out var formDef))
            {
                formDef = resolver.GetRepository<FormDef>().Get(formId)
                    ?? throw new InvalidOperationException("表单定义不存在");
                dataContext.FormDefs.Add(formId, formDef);
            }

            return formDef;
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

            var baseUrl = resolver.GetAppSetting().WebHost.BaseUrl?.TrimEnd('/');
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
