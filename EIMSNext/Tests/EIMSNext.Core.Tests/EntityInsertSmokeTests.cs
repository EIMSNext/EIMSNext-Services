using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 守门用例：模型里的<b>每一张表都必须真的能写进去一行</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为什么需要它：<c>SchemaConsistencyTests</c> 只能证明「模型与库的表/列/类型一致」，
    /// 证明不了「这行数据能插进去」。而这一轮迁移里真实踩到过两类只在写入时才会暴露的问题：
    /// </para>
    /// <list type="number">
    /// <item><description>列声明为 <c>jsonb not null</c> 但 CLR 属性是 <c>string</c>，
    /// 默认值 <c>""</c> 不是合法 JSON，插入报
    /// <c>invalid input syntax for type json</c>；
    /// 若属性为 null 则报 <c>violates not-null constraint</c>。</description></item>
    /// <item><description>可空的内嵌对象属性（<c>Metadata</c>、<c>EventSetting</c> 之类）
    /// 被映射成非空 jsonb 列。</description></item>
    /// </list>
    /// <para>
    /// 做法：对每个映射到表的实体类型 <c>Activator.CreateInstance</c> 造一个「全默认值」实例，
    /// 补一个主键后 <c>Add</c> + <c>SaveChanges</c>，随后<b>回滚</b>（每条用例各自一个事务，
    /// 不提交），因此不会往测试库里留数据。属性初始化器本身就是业务代码的默认值，
    /// 用它来验证「最省字段的一次插入」能否成功。
    /// </para>
    /// <para>
    /// 有意跳过的：映射成视图的只读类型（<c>GetViewName()</c> 非空），它们本来就不写。
    /// </para>
    /// </remarks>
    [TestClass]
    public sealed class EntityInsertSmokeTests
    {
        [TestMethod]
        public void EveryMappedEntityCanBeInserted()
        {
            using var db = TestDbFactory.Create();
            const string smokeDepartmentId = "__entity_insert_smoke_department";
            db.Departments.IgnoreQueryFilters().Where(x => x.Id == smokeDepartmentId).ExecuteDelete();
            db.Departments.Add(new EIMSNext.Entities.Department
            {
                Id = smokeDepartmentId,
                Code = smokeDepartmentId,
                Name = "Entity insert smoke dependency",
                HeriarchyId = $"|{smokeDepartmentId}|",
            });
            db.SaveChanges();

            // 员工（已提交）：EmployeeDepartment / EmployeeGroupMember 经本次改造后
            // 以 EmployeeId 为外键指向 Employee，冒烟插入这两个关系表时必须提供合法的 Employee 主体。
            const string smokeEmployeeId = "__entity_insert_smoke_employee";
            db.Employees.IgnoreQueryFilters().Where(x => x.Id == smokeEmployeeId).ExecuteDelete();
            db.Employees.Add(new EIMSNext.Entities.Employee
            {
                Id = smokeEmployeeId,
                Code = smokeEmployeeId,
                EmpName = "Entity insert smoke employee",
            });
            db.SaveChanges();

            var failures = new List<string>();
            var checkedCount = 0;

            foreach (var entityType in db.Model.GetEntityTypes().OrderBy(x => x.ClrType.FullName, StringComparer.Ordinal))
            {
                var displayName = entityType.ClrType.FullName ?? entityType.ClrType.Name;
                if (entityType.GetTableName() is null) continue;
                // 视图 / 无键只读模型不参与写入。
                if (entityType.GetViewName() is not null) continue;
                if (entityType.FindPrimaryKey() is null) continue;
                if (entityType.ClrType.GetConstructor(Type.EmptyTypes) is null)
                {
                    failures.Add($"{displayName}：缺少无参构造函数，无法构造测试实例。");
                    continue;
                }

                checkedCount++;
                var instance = Activator.CreateInstance(entityType.ClrType)!;
                AssignPrimaryKey(entityType, instance);
                if (instance is EIMSNext.Entities.EmployeeDepartment employeeDepartment)
                {
                    employeeDepartment.DepartmentId = smokeDepartmentId;
                    employeeDepartment.EmployeeId = smokeEmployeeId;
                    employeeDepartment.HeriarchyId = $"|{smokeDepartmentId}|";
                }
                else if (instance is EIMSNext.Entities.EmployeeGroupMember employeeGroupMember)
                {
                    employeeGroupMember.EmployeeId = smokeEmployeeId;
                }

                using var transaction = db.Database.BeginTransaction();
                try
                {
                    db.Add(instance);
                    db.SaveChanges();
                }
                catch (Exception exception)
                {
                    failures.Add($"{displayName}（表 \"{entityType.GetTableName()}\"）：{FirstLine(exception)}");
                }
                finally
                {
                    // 不提交：所有冒烟写入一律丢弃，测试库保持干净。
                    db.ChangeTracker.Clear();
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (InvalidOperationException)
                    {
                        // SaveChanges 失败时事务已被判定为中止，回滚本身可能报错，忽略。
                    }
                }
            }

            Assert.IsTrue(checkedCount > 50, $"只检查到 {checkedCount} 个实体，模型可能没建起来。");
            db.Departments.IgnoreQueryFilters().Where(x => x.Id == smokeDepartmentId).ExecuteDelete();
            db.Employees.IgnoreQueryFilters().Where(x => x.Id == smokeEmployeeId).ExecuteDelete();
            if (failures.Count == 0) return;

            Assert.Fail(
                $"{failures.Count} 个实体写入失败（共检查 {checkedCount} 个）。" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        /// <summary>给实例补一个合法主键，避免所有实体都落在 <c>Id = ''</c> 上。</summary>
        private static void AssignPrimaryKey(IEntityType entityType, object instance)
        {
            var idProperty = entityType.FindProperty("Id");
            if (idProperty?.PropertyInfo is null) return;
            if (idProperty.PropertyInfo.PropertyType != typeof(string)) return;

            idProperty.PropertyInfo.SetValue(instance, Guid.NewGuid().ToString("N"));
        }

        /// <summary>取异常信息的第一行，避免把整个 Npgsql 堆栈塞进断言消息。</summary>
        private static string FirstLine(Exception exception)
        {
            var message = exception.GetBaseException().Message;
            var index = message.IndexOf('\n');
            return index < 0 ? message : message[..index].TrimEnd('\r');
        }
    }
}
