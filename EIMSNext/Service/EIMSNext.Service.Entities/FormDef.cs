using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using EIMSNext.Common;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单定义
    /// </summary>
    public class FormDef : CorpEntityBase
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 模板Id, 对于从模板安装的表单
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// 表单名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

         /// <summary>
        /// 表单内容
        /// </summary>
        public FormContent Content { get; set; } = new FormContent();

        /// <summary>
        /// 是否台账， 台账不支持手动增删改？？？
        /// </summary>
        public bool IsLedger { get; set; }

        /// <summary>
        /// 是否流程表单
        /// </summary>
        public bool UsingWorkflow { get; set; }
    }

    /// <summary>
    /// 自定义表单类型
    /// </summary>
    public enum FormType
    {
        /// <summary>
        /// 表单
        /// </summary>
        Form,
        /// <summary>
        /// 仪表盘
        /// </summary>
        Dashboard,
        /// <summary>
        /// 表单分组
        /// </summary>
        Group
    }

    /// <summary>
    /// 表单定义内容，包括修改历史等
    /// </summary>
    public class FormContent
    {
        /// <summary>
        /// 表单布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;
        /// <summary>
        /// 表单设置
        /// </summary>
        public string Options { get; set; } = string.Empty;
        /// <summary>
        /// 表单组件（仅表单元素，不包含布局组件）
        /// </summary>
        public IList<FieldDef>? Items { get; set; }
    }

    /// <summary>
    /// 字段定义
    /// </summary>
    public class FieldDef
    {
        /// <summary>
        /// 字段名
        /// </summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型
        /// </summary>
        public string Type { get; set; } = FieldType.Input;
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 标题多语言Key
        /// </summary>
        public string? I18n { get; set; }
        /// <summary>
        /// 属性配置
        /// </summary>
        public FieldProp Props { get; set; } = new FieldProp();

        /// <summary>
        /// 子表单中的列
        /// </summary>
        public IList<FieldDef>? Columns { get; set; }

        /// <summary>
        /// 是否隐藏
        /// </summary>
        public bool Hidden { get; set; }
    }

    /// <summary>
    /// 字段属性配置
    /// </summary>
    public class FieldProp
    {
        /// <summary>
        /// Radio/Checkbox/Select/Select2预设的选项
        /// </summary>
        public List<ValueOption>? Options { get; set; }
        /// <summary>
        /// Number/Timestamp的格式
        /// </summary>
        public string? Format { get; set; }
        /// <summary>
        /// 值配置
        /// </summary>
        public ValueProp? ValueProp { get; set; }
    }
    /// <summary>
    /// 值选项
    /// </summary>
    public class ValueOption
    {
        /// <summary>
        /// 选项值
        /// </summary>
        public string Value {  get; set; } = string.Empty;
        /// <summary>
        /// 选项显示文本
        /// </summary>
        public string Label {  get; set; } = string.Empty;
    }
    /// <summary>
    /// 值配置
    /// </summary>
    public class ValueProp
    {
        /// <summary>
        /// 值公式
        /// </summary>
        public string? Formula { get; set; }
        /// <summary>
        /// 公式依赖
        /// </summary>
        public string? Depends { get; set; }
    }

    /// <summary>
    /// 字段变更日志
    /// </summary>
    public class FieldChangeLog
    {
        /// <summary>
        /// 变动类型
        /// </summary>
        public FieldChangeType ChangeType { get; set; }
        /// <summary>
        /// 变更时间
        /// </summary>
        public long ChangeTime { get; set; }
        /// <summary>
        /// 字段定义
        /// </summary>
        public FieldDef Field { get; set; } = new FieldDef();
        /// <summary>
        /// 操作人
        /// </summary>
        public Operator ChangedBy { get; set; } = Operator.Empty;
    }

    /// <summary>
    /// 变动类型
    /// </summary>
    public enum FieldChangeType
    {
        /// <summary>
        /// 移除
        /// </summary>
        Remove,
        /// <summary>
        /// 恢复
        /// </summary>
        Restore
    }
}
