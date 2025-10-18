using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Common
{
    public static class Fields
    {
        public const string BsonId = "_id";
        public const string Id = "Id";
        public const string CreateBy = "CreateBy";
        public const string CreateTime = "CreateTime";
        public const string UpdateBy = "UpdateBy";
        public const string UpdateTime = "UpdateTime";
        public const string IsDeleted = "IsDeleted";
    }

    public static class FieldType
    {
        public const string Input = "input";
        public const string InputNumber = "inputnumber";
        public const string DatePicker = "datePicker";
        public const string Phone = "phone";
        public const string Email = "email";
        public const string TextArea = "textarea";
        public const string Radio = "radio";
        public const string CheckBox = "checkbox";
        public const string Select = "select";
        public const string Employee = "employee";
        public const string Department = "department";
        public const string Address = "address";
        public const string Location = "location";
        public const string Pictures = "pictures";
        public const string Files = "files";
        public const string Signature = "signature";
        //public const string TableForm = "tableForm";
        public const string TableFormPro = "tableFormPro";
    }
}
