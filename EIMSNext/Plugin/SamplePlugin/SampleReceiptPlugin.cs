using System.Composition;

using EIMSNext.Plugin.Contracts;

namespace SamplePlugin
{
    [Export(typeof(IPlugin))]
    [Plugin("sampleplugin", "示例插件", Version = "1.0", Description = "示例插件，用于验证插件节点输入输出配置")]
    public sealed class SampleReceiptPlugin : PluginBase<SampleReceiptPluginSetting>
    {
        [PluginFunction("EchoReceipt", "收款单回显", Description = "演示插件字段映射、执行结果开放字段与下游节点联动")]
        private ReceiptEchoResult EchoReceipt(SampleReceiptArgs args)
        {
            return new ReceiptEchoResult
            {
                Message = "sample receipt plugin executed",
                Code = 0,
                WorkflowId = Context?.Items.TryGetValue("workflowId", out var workflowId) == true ? workflowId?.ToString() : null,
                EchoBizNo = args.BizNo,
                EchoAmount = args.Amount,
                EchoBizDate = args.BizDate,
                EchoRemark = args.Remark,
                EchoStatus = args.Status,
                EchoReceiver = args.Receiver,
                EchoDept = args.Dept,
                EchoAttachments = args.Attachments,
                EchoImages = args.Images,
                EchoItems = args.Items,
            };
        }

        [PluginFunction("EchoMixedData", "通用字段回显", Description = "用于验证插件切换方法、字段重置和结果字段选择")]
        private MixedEchoResult EchoMixedData(MixedEchoArgs args)
        {
            return new MixedEchoResult
            {
                Message = "sample mixed plugin executed",
                EchoTitle = args.Title,
                EchoDescription = args.Description,
                EchoOwner = args.Owner,
                EchoOwnerDept = args.OwnerDept,
            };
        }
    }

    public sealed class SampleReceiptPluginSetting
    {
    }

    public sealed class SampleReceiptArgs : PluginSubList<SampleReceiptItemArgs>
    {
        [PluginInput("单据编号", PluginFieldKind.Text, Key = "bizNo", Required = true)]
        public string? BizNo { get; set; }

        [PluginInput("金额", PluginFieldKind.Number, Key = "amount", Required = true)]
        public decimal Amount { get; set; }

        [PluginInput("业务日期", PluginFieldKind.Timestamp, Key = "bizDate")]
        public long? BizDate { get; set; }

        [PluginInput("备注", PluginFieldKind.TextArea, Key = "remark")]
        public string? Remark { get; set; }

        [PluginInput("状态", PluginFieldKind.SingleSelect, Key = "status", CompatibleFieldTypes = [PluginFieldKind.Radio])]
        public string? Status { get; set; }

        [PluginInput("经办人", PluginFieldKind.SingleEmployee, Key = "receiver")]
        public EmployeeRef? Receiver { get; set; }

        [PluginInput("部门", PluginFieldKind.SingleDepartment, Key = "dept")]
        public DepartmentRef? Dept { get; set; }

        [PluginInput("附件", PluginFieldKind.FileUpload, Key = "attachments")]
        public List<string> Attachments { get; set; } = [];

        [PluginInput("图片", PluginFieldKind.ImageUpload, Key = "images")]
        public List<string> Images { get; set; } = [];

        [PluginSubList("明细子表", Key = "items")]
        public List<SampleReceiptItemArgs> Items { get; set; } = [];
    }

    public sealed class SampleReceiptItemArgs : PluginField
    {
        [PluginInput("项目名称", PluginFieldKind.Text, Key = "itemName")]
        [PluginOutput("项目名称", PluginFieldKind.Text, Key = "itemName")]
        public string? ItemName { get; set; }

        [PluginInput("数量", PluginFieldKind.Number, Key = "qty")]
        [PluginOutput("数量", PluginFieldKind.Number, Key = "qty")]
        public decimal Qty { get; set; }

        [PluginInput("单价", PluginFieldKind.Number, Key = "price")]
        [PluginOutput("单价", PluginFieldKind.Number, Key = "price")]
        public decimal Price { get; set; }

        [PluginInput("费用类别", PluginFieldKind.SingleSelect, Key = "category", CompatibleFieldTypes = [PluginFieldKind.Radio])]
        [PluginOutput("费用类别", PluginFieldKind.SingleSelect, Key = "category")]
        public string? Category { get; set; }

        [PluginInput("费用负责人", PluginFieldKind.SingleEmployee, Key = "costOwner")]
        [PluginOutput("费用负责人", PluginFieldKind.SingleEmployee, Key = "costOwner")]
        public EmployeeRef? CostOwner { get; set; }

        [PluginInput("费用部门", PluginFieldKind.SingleDepartment, Key = "costDept")]
        [PluginOutput("费用部门", PluginFieldKind.SingleDepartment, Key = "costDept")]
        public DepartmentRef? CostDept { get; set; }

        [PluginInput("凭证附件", PluginFieldKind.FileUpload, Key = "evidenceFiles")]
        [PluginOutput("凭证附件", PluginFieldKind.FileUpload, Key = "evidenceFiles")]
        public List<string> EvidenceFiles { get; set; } = [];

        [PluginInput("备注", PluginFieldKind.TextArea, Key = "remark")]
        [PluginOutput("备注", PluginFieldKind.TextArea, Key = "remark")]
        public string? Remark { get; set; }
    }

    public sealed class MixedEchoArgs : PluginField
    {
        [PluginInput("标题", PluginFieldKind.Text, Key = "title", Required = true)]
        public string? Title { get; set; }

        [PluginInput("描述", PluginFieldKind.TextArea, Key = "description")]
        public string? Description { get; set; }

        [PluginInput("负责人", PluginFieldKind.SingleEmployee, Key = "owner")]
        public EmployeeRef? Owner { get; set; }

        [PluginInput("归属部门", PluginFieldKind.SingleDepartment, Key = "ownerDept")]
        public DepartmentRef? OwnerDept { get; set; }
    }

    public sealed class ReceiptEchoResult : PluginSubList<SampleReceiptItemArgs>
    {
        [PluginOutput("返回信息", PluginFieldKind.Text, Key = "message")]
        public string? Message { get; set; }

        [PluginOutput("返回代码", PluginFieldKind.Number, Key = "code")]
        public int Code { get; set; }

        [PluginOutput("流程ID", PluginFieldKind.Text, Key = "workflowId")]
        public string? WorkflowId { get; set; }

        [PluginOutput("回显单号", PluginFieldKind.Text, Key = "echoBizNo")]
        public string? EchoBizNo { get; set; }

        [PluginOutput("回显金额", PluginFieldKind.Number, Key = "echoAmount")]
        public decimal EchoAmount { get; set; }

        [PluginOutput("回显日期", PluginFieldKind.Timestamp, Key = "echoBizDate")]
        public long? EchoBizDate { get; set; }

        [PluginOutput("回显备注", PluginFieldKind.TextArea, Key = "echoRemark")]
        public string? EchoRemark { get; set; }

        [PluginOutput("回显状态", PluginFieldKind.SingleSelect, Key = "echoStatus")]
        public string? EchoStatus { get; set; }

        [PluginOutput("回显经办人", PluginFieldKind.SingleEmployee, Key = "echoReceiver")]
        public EmployeeRef? EchoReceiver { get; set; }

        [PluginOutput("回显部门", PluginFieldKind.SingleDepartment, Key = "echoDept")]
        public DepartmentRef? EchoDept { get; set; }

        [PluginOutput("回显附件", PluginFieldKind.FileUpload, Key = "echoAttachments")]
        public List<string> EchoAttachments { get; set; } = [];

        [PluginOutput("回显图片", PluginFieldKind.ImageUpload, Key = "echoImages")]
        public List<string> EchoImages { get; set; } = [];

        [PluginSubList("回显明细", Key = "echoItems")]
        public List<SampleReceiptItemArgs> EchoItems { get; set; } = [];
    }

    public sealed class MixedEchoResult : PluginField
    {
        [PluginOutput("返回信息", PluginFieldKind.Text, Key = "message")]
        public string? Message { get; set; }

        [PluginOutput("回显标题", PluginFieldKind.Text, Key = "echoTitle")]
        public string? EchoTitle { get; set; }

        [PluginOutput("回显描述", PluginFieldKind.TextArea, Key = "echoDescription")]
        public string? EchoDescription { get; set; }

        [PluginOutput("回显负责人", PluginFieldKind.SingleEmployee, Key = "echoOwner")]
        public EmployeeRef? EchoOwner { get; set; }

        [PluginOutput("回显归属部门", PluginFieldKind.SingleDepartment, Key = "echoOwnerDept")]
        public DepartmentRef? EchoOwnerDept { get; set; }
    }
}
