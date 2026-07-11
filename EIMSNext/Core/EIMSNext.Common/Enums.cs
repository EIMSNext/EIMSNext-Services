namespace EIMSNext.Common
{
    /// <summary>
    /// 操作权限标志。
    /// 注意：不再包含 <c>Write</c>；POST/PATCH/PUT/DELETE 等写操作按场景拆分为
    /// <see cref="Add"/> / <see cref="Edit"/> / <see cref="Delete"/> / <see cref="Import"/>。
    /// </summary>
    [Flags]
    public enum Operation
    {
        /// <summary>未设置 — 等同于不检查。</summary>
        NotSet = 0,

        /// <summary>读（GET / Query / OData Get）。</summary>
        Read = 1,

        /// <summary>新增（POST 创建）。原 Write 在创建场景下的细分。</summary>
        Add = 2,

        /// <summary>修改（PATCH / PUT 更新）。原 Write 在更新场景下的细分。</summary>
        Edit = 4,

        /// <summary>删除（DELETE）。</summary>
        Delete = 8,

        /// <summary>导入（批量 / Excel / API 接入等 Import 端点）。</summary>
        Import = 16,
    }
}
