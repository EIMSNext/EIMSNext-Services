using EIMSNext.Core.Query;
using EIMSNext.Entities;
using Microsoft.EntityFrameworkCore;

using MSTest = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests;

/// <summary>
/// jsonb 数组列「包含某元素」的下推必须生成 <c>@&gt;</c> 运算符（<c>jsonb @&gt; jsonb_build_array(...)</c>）
/// 而不是函数调用，才能命中 jsonb_path_ops GIN 索引（jsonb_path_ops 只服务 @&gt;）。
/// 本用例锁定翻译形态；GIN 命中在 EIMS 验收库（含 IX_TenantAdminGroup_EmployeeIds_Gin）手动验证。
/// </summary>
[TestClass]
public sealed class JsonbArrayContainsTests
{
    [MSTest.TestMethod]
    public void JsonbArrayContains_ProducesAtContainsOperator_NotFunctionCall()
    {
        using var db = TestDbFactory.Create();
        var sql = db.Set<TenantAdminGroup>()
            .Where(x => PgJsonFunctions.JsonbArrayContains(x.EmployeeIds, "emp-1"))
            .ToQueryString();

        MSTest.Assert.IsTrue(sql.Contains("@>"), $"应生成 @> 运算符，实际 SQL：\n{sql}");
        MSTest.Assert.IsTrue(sql.Contains("jsonb_build_array"), $"应构造数组常量，实际 SQL：\n{sql}");
        MSTest.Assert.IsFalse(sql.Contains("eims_json_array_contains"),
            $"不应走函数调用，实际 SQL：\n{sql}");
    }

    [MSTest.TestMethod]
    public void JsonbArrayContains_CombinedWithOtherPredicates_StillUsesOperator()
    {
        using var db = TestDbFactory.Create();
        var sql = db.Set<TenantAdminGroup>()
            .Where(x => x.CorpId == "corp-1" && !x.DeleteFlag
                && PgJsonFunctions.JsonbArrayContains(x.EmployeeIds, "emp-1"))
            .ToQueryString();

        MSTest.Assert.IsTrue(sql.Contains("@>"), $"应生成 @> 运算符，实际 SQL：\n{sql}");
        MSTest.Assert.IsFalse(sql.Contains("eims_json_array_contains"),
            $"不应走函数调用，实际 SQL：\n{sql}");
    }
}
