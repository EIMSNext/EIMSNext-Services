using System.Composition;

using EIMSNext.Common;
using EIMSNext.Plugin.Contracts;

namespace SamplePlugin
{
    [Export(typeof(IPlugin))]
    public sealed class SampleReceiptPlugin : PluginBase<SampleReceiptPluginSetting>
    {
        protected override PluginDesc BuildPluginDesc()
        {
            var desc = new PluginDesc
            {
                Id = "sampleplugin",
                Name = "示例插件",
                Version = "1.0",
                Description = "示例插件，用于验证插件节点输入输出配置"
            };

            var receiptFunction = new FunctionDesc
            {
                Id = "EchoReceipt",
                Name = "收款单回显",
                Description = "演示插件字段映射、执行结果开放字段与下游节点联动"
            };
            receiptFunction.InputFields.Add(CreateField("bizNo", "单据编号", FieldType.Input, required: true));
            receiptFunction.InputFields.Add(CreateField("amount", "金额", FieldType.Number, required: true));
            receiptFunction.InputFields.Add(CreateField("bizDate", "业务日期", FieldType.TimeStamp));
            receiptFunction.InputFields.Add(CreateField("remark", "备注", FieldType.TextArea));
            receiptFunction.InputFields.Add(CreateField("status", "状态", FieldType.Select1, compatibleFieldTypes: [FieldType.Radio]));
            receiptFunction.InputFields.Add(CreateField("receiver", "经办人", FieldType.Employee1));
            receiptFunction.InputFields.Add(CreateField("dept", "部门", FieldType.Department1));
            receiptFunction.InputFields.Add(CreateField("attachments", "附件", FieldType.FileUpload));
            receiptFunction.InputFields.Add(CreateField("images", "图片", FieldType.ImageUpload));
            receiptFunction.InputFields.Add(CreateField("items", "明细子表", FieldType.TableForm));
            receiptFunction.ResultFields.Add(CreateResultField("message", "返回信息", FieldType.Input));
            receiptFunction.ResultFields.Add(CreateResultField("code", "返回代码", FieldType.Number));
            receiptFunction.ResultFields.Add(CreateResultField("workflowId", "流程ID", FieldType.Input));
            receiptFunction.ResultFields.Add(CreateResultField("echoBizNo", "回显单号", FieldType.Input));
            receiptFunction.ResultFields.Add(CreateResultField("echoAmount", "回显金额", FieldType.Number));
            desc.Functions.Add(receiptFunction);

            var mixedFunction = new FunctionDesc
            {
                Id = "EchoMixedData",
                Name = "通用字段回显",
                Description = "用于验证插件切换方法、字段重置和结果字段选择"
            };
            mixedFunction.InputFields.Add(CreateField("title", "标题", FieldType.Input, required: true));
            mixedFunction.InputFields.Add(CreateField("description", "描述", FieldType.TextArea));
            mixedFunction.InputFields.Add(CreateField("owner", "负责人", FieldType.Employee1));
            mixedFunction.InputFields.Add(CreateField("ownerDept", "归属部门", FieldType.Department1));
            mixedFunction.ResultFields.Add(CreateResultField("message", "返回信息", FieldType.Input));
            mixedFunction.ResultFields.Add(CreateResultField("echoTitle", "回显标题", FieldType.Input));
            mixedFunction.ResultFields.Add(CreateResultField("echoOwner", "回显负责人", FieldType.Employee1));
            desc.Functions.Add(mixedFunction);

            return desc;
        }

        private static PluginFieldDesc CreateField(string key, string name, string fieldType, bool required = false, params string[] compatibleFieldTypes)
        {
            var field = new PluginFieldDesc
            {
                Key = key,
                Name = name,
                FieldType = fieldType,
                Required = required,
                AllowCustomValue = true,
                AllowFieldMapping = true,
            };

            foreach (var compatibleType in compatibleFieldTypes)
            {
                field.CompatibleFieldTypes.Add(compatibleType);
            }

            return field;
        }

        private static PluginResultFieldDesc CreateResultField(string key, string name, string fieldType)
        {
            return new PluginResultFieldDesc
            {
                Key = key,
                Name = name,
                FieldType = fieldType,
            };
        }

        private object EchoReceipt(SampleReceiptArgs args)
        {
            return new
            {
                message = "sample receipt plugin executed",
                code = 0,
                workflowId = Context?.Items.TryGetValue("workflowId", out var workflowId) == true ? workflowId : null,
                echoBizNo = args.BizNo,
                echoAmount = args.Amount,
                echoBizDate = args.BizDate,
                echoRemark = args.Remark,
                echoStatus = args.Status,
                echoReceiver = args.Receiver,
                echoDept = args.Dept,
                echoAttachments = args.Attachments,
                echoImages = args.Images,
                echoItems = args.Items,
            };
        }

        private object EchoMixedData(MixedEchoArgs args)
        {
            return new
            {
                message = "sample mixed plugin executed",
                echoTitle = args.Title,
                echoDescription = args.Description,
                echoOwner = args.Owner,
                echoOwnerDept = args.OwnerDept,
            };
        }
    }

    public sealed class SampleReceiptPluginSetting
    {
    }

    public sealed class SampleReceiptArgs
    {
        public string? BizNo { get; set; }
        public decimal Amount { get; set; }
        public DateTime? BizDate { get; set; }
        public string? Remark { get; set; }
        public object? Status { get; set; }
        public object? Receiver { get; set; }
        public object? Dept { get; set; }
        public object? Attachments { get; set; }
        public object? Images { get; set; }
        public List<SampleReceiptItemArgs> Items { get; set; } = new();
    }

    public sealed class SampleReceiptItemArgs
    {
        public string? ItemName { get; set; }
        public decimal Qty { get; set; }
        public decimal Price { get; set; }
        public string? Remark { get; set; }
    }

    public sealed class MixedEchoArgs
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public object? Owner { get; set; }
        public object? OwnerDept { get; set; }
    }
}
