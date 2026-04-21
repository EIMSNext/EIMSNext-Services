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
                Description = "示例收款单插件"
            };

            var function = new FunctionDesc
            {
                Id = "CreateReceipt",
                Name = "收款单新版",
                Description = "演示插件字段配置与映射"
            };
            function.InputFields.Add(CreateField("bizNo", "单据编号", FieldType.Input, required: true));
            function.InputFields.Add(CreateField("amount", "金额", FieldType.Number, required: true));
            function.InputFields.Add(CreateField("bizDate", "业务日期", FieldType.TimeStamp));
            function.InputFields.Add(CreateField("remark", "备注", FieldType.TextArea));
            function.InputFields.Add(CreateField("status", "状态", FieldType.Select1, compatibleFieldTypes: [FieldType.Radio]));
            function.InputFields.Add(CreateField("receiver", "经办人", FieldType.Employee1));
            function.InputFields.Add(CreateField("dept", "部门", FieldType.Department1));
            function.InputFields.Add(CreateField("attachments", "附件", FieldType.FileUpload));
            function.InputFields.Add(CreateField("images", "图片", FieldType.ImageUpload));
            desc.Functions.Add(function);

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

        private object CreateReceipt(SampleReceiptArgs args)
        {
            return new
            {
                success = true,
                message = "sample plugin executed",
                args.BizNo,
                args.Amount,
                args.BizDate,
                args.Remark,
                args.Status,
                args.Receiver,
                args.Dept,
                args.Attachments,
                args.Images,
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
    }
}
