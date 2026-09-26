using EIMSNext.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 守门用例：所有字符列都是 citext，标识符列（主键与外键）额外挂 C 排序规则。
    /// </summary>
    /// <remarks>
    /// 大小写由列类型承担后，查询与写路径都不再需要 ToLower() 归一化；一并断言的还有「数据库里真的
    /// 不区分大小写」，防止有人把折叠写回 LINQ，或把标识符列退回 text。
    /// </remarks>
    [TestClass]
    public sealed class CaseInsensitiveColumnTests
    {
        /// <summary>模型契约：字符列一律 citext，标识符列再挂 C。</summary>
        [TestMethod]
        public void CharacterColumnsUseCitext()
        {
            using var db = TestDbFactory.Create();

            // 列类型与排序规则属于「非运行时优化」的元数据，运行时模型里读不到，必须取设计时模型。
            var model = db.GetService<IDesignTimeModel>().Model;

            var user = model.FindEntityType(typeof(User))!;
            Assert.AreEqual("citext", user.FindProperty(nameof(User.Email))!.GetColumnType());
            Assert.AreEqual("citext", user.FindProperty(nameof(User.Name))!.GetColumnType());
            Assert.AreEqual(
                "citext",
                user.FindProperty(nameof(User.Platform))!.GetColumnType(),
                "以文本落库的枚举列也应是 citext。");

            var employee = model.FindEntityType(typeof(Employee))!;
            Assert.AreEqual("citext", employee.FindProperty("Id")!.GetColumnType());
            Assert.AreEqual("C", employee.FindProperty("Id")!.GetCollation());
            Assert.AreEqual("citext", employee.FindProperty("CorpId")!.GetColumnType());
            Assert.AreEqual("C", employee.FindProperty("CorpId")!.GetCollation());

            var relation = model.FindEntityType(typeof(EmployeeDepartment))!;
            Assert.AreEqual("citext", relation.FindProperty(nameof(EmployeeDepartment.DepartmentId))!.GetColumnType());
            Assert.AreEqual("C", relation.FindProperty(nameof(EmployeeDepartment.DepartmentId))!.GetCollation());
        }

        /// <summary>真实库行为：混大小写落库的邮箱，用任意大小写都能等值命中。</summary>
        [TestMethod]
        public void EmailEqualityIsCaseInsensitiveInDatabase()
        {
            using var db = TestDbFactory.Create();
            using var transaction = db.Database.BeginTransaction();

            // 只给 Id/Email/Name/Platform，其余 NOT NULL 文本列由 001 基线脚本补的 default '' 兜底。
            db.Database.ExecuteSqlRaw(
                """insert into "User" ("Id", "Email", "Phone", "Name", "Platform") values ('zz-ci-column-user', 'MixedCase@Example.COM', '', 'ci', 'Public')""");

            Assert.IsNotNull(
                db.Users.AsNoTracking().FirstOrDefault(x => x.Email == "mixedcase@example.com"),
                "小写查询应命中混大小写落库的行。");
            Assert.IsNotNull(
                db.Users.AsNoTracking().FirstOrDefault(x => x.Email == "MIXEDCASE@EXAMPLE.COM"),
                "全大写查询应命中混大小写落库的行。");

            transaction.Rollback();
        }

        /// <summary>真实库行为：主键 Id 的等值查询同样不区分大小写。</summary>
        [TestMethod]
        public void IdEqualityIsCaseInsensitiveInDatabase()
        {
            using var db = TestDbFactory.Create();
            using var transaction = db.Database.BeginTransaction();

            db.Database.ExecuteSqlRaw(
                """insert into "User" ("Id", "Email", "Phone", "Name", "Platform") values ('Zz-Ci-Id-User', 'zz-ci-id@example.com', '', 'ci', 'Public')""");

            Assert.IsNotNull(
                db.Users.AsNoTracking().FirstOrDefault(x => x.Id == "zz-ci-id-user"),
                "小写 Id 查询应命中混大小写落库的主键。");

            transaction.Rollback();
        }
    }
}
