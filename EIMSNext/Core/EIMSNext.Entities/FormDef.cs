using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations.Schema;

using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 表单定义
    /// </summary>
    public class FormDef : CorpEntityBase
    {
        /// <summary>
        /// 是否为跨应用绑定进来的外部表单（仅查询结果填充，非持久化字段）。
        /// </summary>
        [NotMapped]
        public bool External { get; set; }
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
        /// 是否流程表单
        /// </summary>
        public bool UsingWorkflow { get; set; }

        /// <summary>
        /// 表单设置
        /// </summary>
        public FormSettings FormSettings { get; set; } = new FormSettings();

        /// <summary>
        /// 公开表单可直接查询的关联数据源表单。仅后端持久化和鉴权使用。
        /// </summary>
        [JsonIgnore]
        public List<string> PublicRelatedFormIds { get; set; } = [];
    }

    /// <summary>
    /// 表单设置。
    /// </summary>
    public class FormSettings
    {
        /// <summary>
        /// 数据标题设置。
        /// </summary>
        public DataTitleSettings DataTitle { get; set; } = new DataTitleSettings();
    }

    /// <summary>
    /// 数据标题设置。
    /// </summary>
    public class DataTitleSettings
    {
        /// <summary>
        /// 标题模式，支持 default/custom。
        /// </summary>
        public string Mode { get; set; } = "default";

        /// <summary>
        /// 自定义标题模板内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// 自定义表单类型
    /// </summary>
    public enum FormType
    {
        /// <summary>
        /// 表单
        /// </summary>
        Form = 0,
        /// <summary>
        /// 仪表盘
        /// </summary>
        Dashboard = 1,
        /// <summary>
        /// 表单分组
        /// </summary>
        Group = 2,
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

        /// <summary>
        /// 已删除字段记录。
        /// </summary>
        public IList<FieldChangeLog> FieldChangeLogs { get; set; } = [];
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
        /// 是否必填。兼容前端 form-create 的 $required 配置。
        /// </summary>
        [JsonPropertyName("$required")]
        public bool Required { get; set; }

        /// <summary>
        /// 子表单中的列
        /// </summary>
        public IList<FieldDef>? Columns { get; set; }

        /// <summary>
        /// 是否隐藏
        /// </summary>
        public bool Hidden { get; set; }

        /// <summary>
        /// 字段来源。public 表示公开发布系统字段。
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// 系统字段分类。
        /// </summary>
        public string? SystemKind { get; set; }
    }

    /// <summary>
    /// 字段属性配置
    /// </summary>
    public class FieldProp
    {
        /// <summary>
        /// 员工/部门组件的数据源范围。该配置保存在 FormContent.Items 中，运行时由服务端解析。
        /// </summary>
        public MemberSource? MemberSource { get; set; }

        /// <summary>
        /// Radio/Checkbox/Select/Select2预设的选项
        /// </summary>
        public List<ValueOption>? Options { get; set; }
        /// <summary>
        /// Number/Timestamp的格式
        /// </summary>
        public string? Format { get; set; }
        /// <summary>
        /// 地址字段的层级：1=省 2=省-市 3=省-市-区 4=省-市-区-详细地址。
        /// 服务端只做搬运，供前端筛选/查询条件按字段类型截断级联层级。
        /// </summary>
        public int? Level { get; set; }
        /// <summary>
        /// 兼容部分子表单列把必填配置存放在 props.required 的情况。
        /// </summary>
        public bool? Required { get; set; }
        /// <summary>
        /// 值配置
        /// </summary>
        public ValueProp? ValueProp { get; set; }
    }

    /// <summary>
    /// 员工/部门组件的数据源配置。
    /// </summary>
    public class MemberSource
    {
        /// <summary>范围模式：all 或 custom。</summary>
        public string Mode { get; set; } = MemberSourceMode.All;

        /// <summary>范围项之间为 OR 关系。</summary>
        public IList<MemberSourceItem> Items { get; set; } = [];
    }

    /// <summary>成员数据源范围模式。</summary>
    public static class MemberSourceMode
    {
        public const string All = "all";
        public const string Custom = "custom";
    }

    /// <summary>成员数据源范围项。</summary>
    public class MemberSourceItem
    {
        /// <summary>department、employeeGroup、employee 或 dynamic。</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>租户内实体 ID，或 dynamic 类型的 curuser/curdept。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>部门项是否包含下级部门。</summary>
        public bool Cascaded { get; set; }
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
    /// 已删除字段记录
    /// </summary>
    public class FieldChangeLog
    {
        /// <summary>
        /// 字段 ID。子表字段使用 parentField&gt;childField。
        /// </summary>
        public string FieldId { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型
        /// </summary>
        public string FieldType { get; set; } = string.Empty;
        /// <summary>
        /// 字段名称。子表字段使用 parentLabel.childLabel。
        /// </summary>
        public string FieldLabel { get; set; } = string.Empty;
        /// <summary>
        /// 删除人
        /// </summary>
        public Operator DeletedBy { get; set; } = Operator.Empty;
        /// <summary>
        /// 删除时间
        /// </summary>
        public long DeletedTime { get; set; }
    }
}
