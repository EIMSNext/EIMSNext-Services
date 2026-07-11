namespace EIMSNext.Common
{
    public static class Constants
    {
        public const string Defaut_MoneyFormat = "0.00";
        public const string Defaut_DateFormat = "yyyy-MM-dd";
        public const string Defaut_DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 5000;
        public const int DefaultTokenLifetime = 28800;
        public const string Read = "read";
        public const string ReadWrite = "readwrite";
        public const string PermissionCacheKey = "userp_";
        public const string NoPassword = "(!@#^&*$%) [,./';:>?<]";
        public static string BaseDirectory = "";
        public static string ContentRootPath = "";
        public static string WebRootPath = "";
        public const string QRCodePath = "qrcode";

        /// <summary>
        /// 所有权限操作的合集（Read + Add + Edit + Delete + Import）。
        /// 已移除 <c>Write</c>，拆分为 4 个细粒度标志。
        /// </summary>
        public static readonly Operation Operation_All = Operation.Read | Operation.Add | Operation.Edit | Operation.Delete | Operation.Import;

        public const string System = "system";
        public const string Id = "Id";

        /// <summary>
        /// 表单数据导入中可在线编辑失败数据的最大条数。
        /// 超出此上限后只生成错误报告 Excel，不再提供重试时的内联编辑。
        /// </summary>
        public const int FormDataImportMaxEditableErrors = 30;
    }
}