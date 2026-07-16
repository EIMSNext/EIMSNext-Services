using System.Text.RegularExpressions;

namespace EIMSNext.Common
{
    public static class Fields
    {
        public const string BsonId = "_id";
        public const string Id = "id";
        public const string DataTitle = "dataTitle";
        public const string CreateBy = "createBy";
        public const string CreateTime = "createTime";
        public const string UpdateBy = "updateBy";
        public const string UpdateTime = "updateTime";
        public const string DeleteFlag = "deleteFlag";
        public const string Data = "data";

        public const string CorpId = "corpId";
        public const string AppId = "appId";
        public const string FormId = "formId";
        public const string FlowStatus = "flowStatus";

        public static readonly string[] SystemFields = { Id, BsonId, DataTitle, CreateBy, CreateTime, UpdateBy, UpdateTime, DeleteFlag, CorpId, AppId, FormId, FlowStatus };
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
    ///  - 不允许出现 <c>$</c>（与 dataflow 公式占位符 <c>$F1</c>/<c>$F2</c> 冲突；
    ///    避免子表单字段引用 <c>{parent}>{child}</c> 之外的意外替换）；
    ///  - 不允许出现 <c>&gt;</c>（dataflow 公式的子表分隔符 <c>parent&gt;child</c>）；
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

    public static class FieldType
    {
        public const string Input = "input";
        public const string Number = "number";
        public const string TimeStamp = "timestamp";
        //public const string Phone = "phone";
        //public const string Email = "email";
        public const string TextArea = "textarea";
        public const string Radio = "radio";
        public const string CheckBox = "checkbox";
        public const string Select1 = "select";
        public const string Select2 = "select2";
        //public const string Address = "address";
        //public const string Location = "location";
        public const string ImageUpload = "imageupload";
        public const string FileUpload = "fileupload";
        public const string Signature = "signature";
        public const string DataSelect = "dataselect";
        public const string TableForm = "tableform";
        public const string Employee1 = "employee1";
        public const string Employee2 = "employee2";
        public const string Department1 = "department1";
        public const string Department2 = "department2";
        /// <summary>
        /// 流水号(自动生成,只读,提交时由后端生成)
        /// </summary>
        public const string SerialNo = "serialno";

        public static readonly string[] AllFieldTypes = [Input, Number, TimeStamp, TextArea, Radio, CheckBox, Select1, Select2, ImageUpload, FileUpload, Signature, DataSelect, TableForm, Employee1, Employee2, Department1, Department2, SerialNo];
        public static bool IsInputField(string type)
        {
            return AllFieldTypes.Contains(type);
        }
    }
}
