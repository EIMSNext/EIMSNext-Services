using System.Linq.Expressions;

using EIMSNext.Entities;

namespace EIMSNext.Service.Tests
{
    /// <summary>
    /// 员工按部门过滤的关系表语义。
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
