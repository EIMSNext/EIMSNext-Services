using System.Text.RegularExpressions;

namespace EIMSNext.Common
{
    /// <summary>
    /// 定义系统字段名常量以及系统字段判断方法。
    /// </summary>
    public static class Fields
    {
        /// <summary>
        /// MongoDB 内部主键字段名。
        /// </summary>
        public const string BsonId = "_id";

        /// <summary>
        /// 主键字段名。
        /// </summary>
        public const string Id = "id";

        /// <summary>
        /// 数据标题字段名。
        /// </summary>
        public const string DataTitle = "dataTitle";

        /// <summary>
        /// 创建人字段名。
        /// </summary>
        public const string CreateBy = "createBy";

        /// <summary>
        /// 创建人 Id 字段名。
        /// </summary>
        public const string CreateById = $"{CreateBy}.{BsonId}";

        /// <summary>
        /// 创建时间字段名。
        /// </summary>
        public const string CreateTime = "createTime";

        /// <summary>
        /// 更新人字段名。
        /// </summary>
        public const string UpdateBy = "updateBy";

        /// <summary>
        /// 更新时间字段名。
        /// </summary>
        public const string UpdateTime = "updateTime";

        /// <summary>
        /// 删除标记字段名。
        /// </summary>
        public const string DeleteFlag = "deleteFlag";

        /// <summary>
        /// 数据字段名。
        /// </summary>
        public const string Data = "data";

        /// <summary>
        /// 企业 Id 字段名。
        /// </summary>
        public const string CorpId = "corpId";

        /// <summary>
        /// 应用 Id 字段名。
        /// </summary>
        public const string AppId = "appId";

        /// <summary>
        /// 表单 Id 字段名。
        /// </summary>
        public const string FormId = "formId";

        /// <summary>
        /// 流程状态字段名。
        /// </summary>
        public const string FlowStatus = "flowStatus";

        /// <summary>
        /// 所有系统字段名列表。
        /// </summary>
        public static readonly string[] SystemFields = { Id, BsonId, DataTitle, CreateBy, CreateTime, UpdateBy, UpdateTime, DeleteFlag, CorpId, AppId, FormId, FlowStatus };

        /// <summary>
        /// 判断指定字段名是否为系统字段。
        /// </summary>
        /// <param name="fieldName">字段名。</param>
        /// <returns>是系统字段时返回 true，否则返回 false。</returns>
        public static bool IsSystemField(string fieldName)
        {
            return SystemFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 字段 ID（<c>FieldDef.Field</c>）的硬性约束。
    /// <para>
    /// 设计器在保存表单时调用 <see cref="ValidateFieldId"/> 拒绝不合规的字段名。
    /// 不变量：
    ///  - 不允许出现 ASCII 控制字符；
    ///  - 不允许出现 <c>$</c>（与 eventFlow 公式占位符 <c>$F1</c>/<c>$F2</c> 冲突；
    ///    避免子表单字段引用 <c>{parent}>{child}</c> 之外的意外替换）；
    ///  - 不允许出现 <c>&gt;</c>（eventFlow 公式的子表分隔符 <c>parent&gt;child</c>）；
    ///  - 不允许出现 ASCII 控制字符与空白。
    /// </para>
    /// <para>
    /// 若字段是子表单的列 ID（即 <c>parent.Field</c> 中的 <c>Field</c> 部分），由
    /// <see cref="ValidateSubFieldId"/> 校验，规则相同。
    /// </para>
    /// </summary>
    public static class FieldIdRules
    {
        private static readonly Regex InvalidCharRegex = new(@"[\x00-\x1F$>\s]", RegexOptions.Compiled);

        /// <summary>
        /// 校验字段 ID 是否合法。返回错误消息；空字符串表示通过。
        /// </summary>
        public static string ValidateFieldId(string? fieldId)
        {
            if (string.IsNullOrWhiteSpace(fieldId))
            {
                return "字段 ID 不能为空";
            }
            if (fieldId.Length > 64)
            {
                return "字段 ID 长度不能超过 64";
            }
            var m = InvalidCharRegex.Match(fieldId);
            if (m.Success)
            {
                return $"字段 ID 包含非法字符 '{m.Value}'（不允许 $, >, 控制字符或空白）";
            }
            return string.Empty;
        }

        /// <summary>
        /// 校验子表单列 ID（不含父前缀）。规则同 <see cref="ValidateFieldId"/>。
        /// </summary>
        public static string ValidateSubFieldId(string? subFieldId) => ValidateFieldId(subFieldId);
    }

    /// <summary>
    /// 定义表单字段类型常量。
    /// </summary>
    public static class FieldType
    {
        /// <summary>
        /// 单行文本输入。
        /// </summary>
        public const string Input = "input";

        /// <summary>
        /// 数值输入。
        /// </summary>
        public const string Number = "number";

        /// <summary>
        /// 时间戳。
        /// </summary>
        public const string TimeStamp = "timestamp";

        /// <summary>
        /// 多行文本。
        /// </summary>
        public const string TextArea = "textarea";

        /// <summary>
        /// 单选按钮。
        /// </summary>
        public const string Radio = "radio";

        /// <summary>
        /// 多选框。
        /// </summary>
        public const string CheckBox = "checkbox";

        /// <summary>
        /// 单选下拉。
        /// </summary>
        public const string Select1 = "select";

        /// <summary>
        /// 多选下拉。
        /// </summary>
        public const string Select2 = "select2";

        /// <summary>
        /// 图片上传。
        /// </summary>
        public const string ImageUpload = "imageupload";

        /// <summary>
        /// 文件上传。
        /// </summary>
        public const string FileUpload = "fileupload";

        /// <summary>
        /// 电子签名。
        /// </summary>
        public const string Signature = "signature";

        /// <summary>
        /// 数据选择。
        /// </summary>
        public const string DataSelect = "dataselect";

        /// <summary>
        /// 子表。
        /// </summary>
        public const string TableForm = "tableform";

        /// <summary>
        /// 员工单选。
        /// </summary>
        public const string Employee1 = "employee1";

        /// <summary>
        /// 员工多选。
        /// </summary>
        public const string Employee2 = "employee2";

        /// <summary>
        /// 部门单选。
        /// </summary>
        public const string Department1 = "department1";

        /// <summary>
        /// 部门多选。
        /// </summary>
        public const string Department2 = "department2";

        /// <summary>
        /// 流水号(自动生成,只读,提交时由后端生成)。
        /// </summary>
        public const string SerialNo = "serialno";

        /// <summary>
        /// 所有支持的字段类型列表。
        /// </summary>
        public static readonly string[] AllFieldTypes = [Input, Number, TimeStamp, TextArea, Radio, CheckBox, Select1, Select2, ImageUpload, FileUpload, Signature, DataSelect, TableForm, Employee1, Employee2, Department1, Department2, SerialNo];

        /// <summary>
        /// 判断指定类型是否为有效的表单字段类型。
        /// </summary>
        /// <param name="type">字段类型字符串。</param>
        /// <returns>是有效字段类型时返回 true，否则返回 false。</returns>
        public static bool IsInputField(string type)
        {
            return AllFieldTypes.Contains(type);
        }
    }
}
