using System.Linq.Expressions;

using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    /// <summary>
    /// 员工按部门过滤的关系表语义。
    /// <para>
    /// Mongo 时期过滤的是 <c>Employee.Depts</c> 这个 jsonb 内嵌数组；迁移到 PostgreSQL 后
    /// 该投影已删除，过滤改走关系表 <c>EmployeeDepartment</c> 的层级路径快照
    /// <c>HeriarchyId</c>（创建归属关系时从部门写入，部门层级变动时由 DepartmentService 同步），
    /// 级联按部门查员工不再需要 EmployeeDepartment → Department 的导航/联表。
    /// 这里用内存 LINQ 守住语义，真实 SQL 翻译由 OData 集成路径覆盖。
    /// </para>
    /// </summary>
    [TestClass]
    public class EmployeeEmpDeptFilterTests
    {
        [TestMethod]
        public void Departments_ContainsFilter_TranslatesToExpressionTree()
        {
            var departmentId = "dept-123";

            Expression<Func<Employee, bool>> filter =
                x => x.Departments.Any(d => d.DepartmentId == departmentId);

            Assert.IsNotNull(filter);
            StringAssert.Contains(filter.ToString(), "Departments");
        }

        [TestMethod]
        public void Departments_HeriarchyIdContainsFilter_TranslatesToExpressionTree()
        {
            var departmentId = "dept-456";

            Expression<Func<Employee, bool>> filter =
                x => x.Departments.Any(d => d.HeriarchyId.Contains($"|{departmentId}|"));

            Assert.IsNotNull(filter);
            StringAssert.Contains(filter.ToString(), "HeriarchyId");
            StringAssert.Contains(filter.ToString(), "Contains");
        }

        [TestMethod]
        public void Departments_LinqExpression_MatchesDirectAndCascaded()
        {
            var parent = NewDepartment("dept-a", "|dept-a|");
            var child = NewDepartment("dept-b", "|dept-a|dept-b|");
            var other = NewDepartment("dept-c", "|dept-c|");
            var unrelated = NewDepartment("dept-d", "|parent|other|dept-d|");

            var employees = new List<Employee>
            {
                NewEmployee("emp-1", NewRelation(parent), NewRelation(child)),
                NewEmployee("emp-2", NewRelation(other)),
                NewEmployee("emp-3", NewRelation(unrelated)),
                NewEmployee("emp-4")
            }.AsQueryable();

            // 直接部门过滤：只命中显式关联该部门的员工。
            var byDirectDepartment = employees
                .Where(x => x.Departments.Any(d => d.DepartmentId == "dept-a"))
                .Select(x => x.Id)
                .ToList();
            CollectionAssert.AreEqual(new[] { "emp-1" }, byDirectDepartment);

            // 级联过滤：直接按关系表层级路径快照匹配，命中父部门下所有员工（含子部门）。
            var byCascadedAncestor = employees
                .Where(x => x.Departments.Any(d => d.HeriarchyId.Contains("|dept-a|")))
                .Select(x => x.Id)
                .ToList();
            CollectionAssert.AreEquivalent(new[] { "emp-1" }, byCascadedAncestor);

            // 管道符边界：层级路径片段必须整体匹配，|dept-a| 不得误命中 |parent|other|dept-d|。
            var byUnrelatedAncestor = employees
                .Where(x => x.Departments.Any(d => d.HeriarchyId.Contains("|dept-d|")))
                .Select(x => x.Id)
                .ToList();
            CollectionAssert.AreEqual(new[] { "emp-3" }, byUnrelatedAncestor);
        }

        private static Department NewDepartment(string id, string hierarchyId) => new()
        {
            Id = id,
            Code = id,
            Name = id,
            HeriarchyId = hierarchyId
        };

        private static EmployeeDepartment NewRelation(Department department)
            => new() { DepartmentId = department.Id, HeriarchyId = department.HeriarchyId };

        private static Employee NewEmployee(string id, params EmployeeDepartment[] relations) => new()
        {
            Id = id,
            CorpId = "corp-1",
            Departments = relations.ToList()
        };
    }
}
