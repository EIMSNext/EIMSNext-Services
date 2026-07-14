using EIMSNext.Service.Entities;

using MongoDB.Driver;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class EmployeeEmpDeptFilterTests
    {
        [TestMethod]
        public void Depts_ContainsFilter_TranslatesToFilterDefinition()
        {
            var departmentId = "dept-123";
            var fb = Builders<Employee>.Filter;

            var filter = fb.ElemMatch(x => x.Depts,
                d => d.DeptId == departmentId);

            Assert.IsNotNull(filter);
        }

        [TestMethod]
        public void Depts_HeriarchyIdContainsFilter_TranslatesToFilterDefinition()
        {
            var departmentId = "dept-456";
            var fb = Builders<Employee>.Filter;

            var filter = fb.ElemMatch(x => x.Depts,
                d => d.HeriarchyId.Contains($"|{departmentId}|"));

            Assert.IsNotNull(filter);
        }

        [TestMethod]
        public void Depts_LinqExpression_WorksInMemory()
        {
            var employees = new List<Employee>
            {
                new Employee
                {
                    Id = "emp-1",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-a", HeriarchyId = "|dept-a|", DeptName = "Dept A" },
                        new EmpDept { DeptId = "dept-b", HeriarchyId = "|dept-a|dept-b|", DeptName = "Dept B" }
                    }
                },
                new Employee
                {
                    Id = "emp-2",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-c", HeriarchyId = "|dept-c|", DeptName = "Dept C" }
                    }
                },
                new Employee
                {
                    Id = "emp-3",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>()
                }
            }.AsQueryable();

            var nonRecursiveResult = employees
                .Where(x => x.Depts.Any(d => d.DeptId == "dept-a"))
                .ToList();
            Assert.AreEqual(1, nonRecursiveResult.Count);
            Assert.AreEqual("emp-1", nonRecursiveResult[0].Id);

            var recursiveResult = employees
                .Where(x => x.Depts.Any(d => d.HeriarchyId.Contains("|dept-a|")))
                .ToList();
            Assert.AreEqual(1, recursiveResult.Count);
            Assert.AreEqual("emp-1", recursiveResult[0].Id);
        }

        [TestMethod]
        public void Depts_LinqExpression_RecursiveWithDescendants()
        {
            var employees = new List<Employee>
            {
                new Employee
                {
                    Id = "emp-1",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-root", HeriarchyId = "|dept-root|", DeptName = "Root" },
                        new EmpDept { DeptId = "dept-child", HeriarchyId = "|dept-root|dept-child|", DeptName = "Child" }
                    }
                },
                new Employee
                {
                    Id = "emp-2",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-child", HeriarchyId = "|dept-root|dept-child|", DeptName = "Child" }
                    }
                },
                new Employee
                {
                    Id = "emp-3",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-other", HeriarchyId = "|dept-other|", DeptName = "Other" }
                    }
                }
            }.AsQueryable();

            var recursiveResult = employees
                .Where(x => x.Depts.Any(d => d.HeriarchyId.Contains("|dept-root|")))
                .ToList();
            Assert.AreEqual(2, recursiveResult.Count);
            CollectionAssert.Contains(recursiveResult.Select(x => x.Id).ToList(), "emp-1");
            CollectionAssert.Contains(recursiveResult.Select(x => x.Id).ToList(), "emp-2");
        }

        [TestMethod]
        public void Depts_LinqExpression_ContainsWithPipeChar()
        {
            var employees = new List<Employee>
            {
                new Employee
                {
                    Id = "emp-1",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-a", HeriarchyId = "|parent|dept-a|", DeptName = "Dept A" }
                    }
                },
                new Employee
                {
                    Id = "emp-2",
                    CorpId = "corp-1",
                    Depts = new List<EmpDept>
                    {
                        new EmpDept { DeptId = "dept-b", HeriarchyId = "|parent|other|dept-b|", DeptName = "Dept B" }
                    }
                }
            }.AsQueryable();

            var result = employees
                .Where(x => x.Depts.Any(d => d.HeriarchyId.Contains("|dept-a|")))
                .ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("emp-1", result[0].Id);

            var parentResult = employees
                .Where(x => x.Depts.Any(d => d.HeriarchyId.Contains("|parent|")))
                .ToList();
            Assert.AreEqual(2, parentResult.Count);
        }
    }
}
