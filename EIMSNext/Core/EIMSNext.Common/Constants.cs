namespace EIMSNext.Common
{
    /// <summary>
    /// 定义系统级公共常量。
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// 默认金额格式。
        /// </summary>
        public const string Defaut_MoneyFormat = "0.00";

        /// <summary>
        /// 默认日期格式。
        /// </summary>
        public const string Defaut_DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// 默认日期时间格式。
        /// </summary>
        public const string Defaut_DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// 默认每页大小。
        /// </summary>
        public const int DefaultPageSize = 20;

        /// <summary>
        /// 最大每页大小。
        /// </summary>
        public const int MaxPageSize = 5000;

        /// <summary>
        /// 令牌默认有效期（秒）。
        /// </summary>
        public const int DefaultTokenLifetime = 28800;

        /// <summary>
        /// 只读权限标识。
        /// </summary>
        public const string Read = "read";

        /// <summary>
        /// 读写权限标识。
        /// </summary>
        public const string ReadWrite = "readwrite";

        /// <summary>
        /// 用户权限缓存键前缀。
        /// </summary>
        public const string PermissionCacheKey = "userp_";

        /// <summary>
        /// 系统保留的口令（永不使用）。
        /// </summary>
        public const string NoPassword = "(!@#^&*$%) [,./';:>?<]";

        /// <summary>
        /// 获取或设置应用程序基目录。
        /// </summary>
        public static string BaseDirectory = "";

        /// <summary>
        /// 获取或设置应用程序内容根路径。
        /// </summary>
        public static string ContentRootPath = "";

        /// <summary>
        /// 获取或设置应用程序 Web 根路径。
        /// </summary>
        public static string WebRootPath = "";

        /// <summary>
        /// 二维码存储路径。
        /// </summary>
        public const string QRCodePath = "qrcode";

        /// <summary>
        /// 所有权限操作的合集（Read + Add + Edit + Delete + Import）。
        /// 已移除 <c>Write</c>，拆分为 4 个细粒度标志。
        /// </summary>
        public static readonly Operation Operation_All = Operation.Read | Operation.Add | Operation.Edit | Operation.Delete | Operation.Import;

        /// <summary>
        /// 系统用户标识。
        /// </summary>
        public const string System = "system";

        /// <summary>
        /// 主键字段名。
        /// </summary>
        public const string Id = "Id";

        /// <summary>
        /// 表单数据导入中可在线编辑失败数据的最大条数。
        /// 超出此上限后只生成错误报告 Excel，不再提供重试时的内联编辑。
        /// </summary>
        public const int FormDataImportMaxEditableErrors = 30;
    }
}
