namespace EIMSNext.Common
{
    public static class Fields
    {
        public const string BsonId = "_id";
        public const string Id = "id";
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

        public static readonly string[] SystemFields = { Id, BsonId, CreateBy, CreateTime, UpdateBy, UpdateTime, DeleteFlag, CorpId, AppId, FormId, FlowStatus };
        public static bool IsSystemField(string fieldName)
        {
            return SystemFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
        }
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
        //public const string Signature = "signature";
        public const string TableForm = "tableform";
        public const string Employee1 = "employee1";
        public const string Employee2 = "employee2";
        public const string Department1 = "department1";
        public const string Department2 = "department2";

        public static readonly string[] AllFieldTypes = [Input, Number, TimeStamp, TextArea, Radio, CheckBox, Select1, Select2, ImageUpload, FileUpload, TableForm, Employee1, Employee2, Department1, Department2];
        public static bool IsInputField(string type)
        {
            return AllFieldTypes.Contains(type);
        }
    }
}
