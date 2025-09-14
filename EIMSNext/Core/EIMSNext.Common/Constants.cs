﻿namespace EIMSNext.Common
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

        public static readonly Operation Operation_All = Operation.Read | Operation.Write;

        public const string System = "system";
        public const string Id = "Id";

        public const string Field_BsonId = "_id";
        public const string Field_Id = "Id";
        public const string Field_CreateBy = "CreateBy";
        public const string Field_CreateTime = "CreateTime";
        public const string Field_UpdateBy = "UpdateBy";
        public const string Field_UpdateTime = "UpdateTime";
        public const string Field_IsDeleted = "IsDeleted";

        public static readonly string[] SystemFields = { Field_Id, Field_BsonId, Field_CreateBy, Field_CreateTime, Field_UpdateBy, Field_UpdateTime, Field_IsDeleted };

    }
}