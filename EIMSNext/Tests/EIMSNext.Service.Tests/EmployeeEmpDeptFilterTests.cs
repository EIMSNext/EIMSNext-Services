using EIMSNext.Service.Entities;

using MongoDB.Driver;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class EmployeeEmpDeptFilterTests
    {
        [TestMethod]
        public void EmpDepts_ContainsFilter_TranslatesToFilterDefinition()
        {
            var departmentId = "dept-123";
            var fb = Builders<Employee>.Filter;

            var filter = fb.ElemMatch(x => x.EmpDepts,
                d => d.Id == departmentId);

            Assert.IsNotNull(filter);
        }

        [TestMethod]
        public void EmpDepts_HeriarchyIdContainsFilter_TranslatesToFilterDefinition()
        {
            var departmentId = "dept-456";
            var fb = Builders<Employee>.Filter;

            var filter = fb.ElemMatch(x => x.EmpDepts,
                d => d.HeriarchyId.Contains($"|{departmentId}|"));

            Assert.IsNotNull(filter);
        }

        [TestMethod]
        public void EmpDepts_LinqExpression_WorksInMemory()
        {
            var employees = new List<Employee>
            {
                new Employee
                {
                    Id = "emp-1",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-a", HeriarchyId = "|dept-a|", Name = "Dept A" },
                        new EmpDept { Id = "dept-b", HeriarchyId = "|dept-a|dept-b|", Name = "Dept B" }
                    }
                },
                new Employee
                {
                    Id = "emp-2",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-c", HeriarchyId = "|dept-c|", Name = "Dept C" }
                    }
                },
                new Employee
                {
                    Id = "emp-3",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>()
                }
            }.AsQueryable();

            var nonRecursiveResult = employees
                .Where(x => x.EmpDepts.Any(d => d.Id == "dept-a"))
                .ToList();
            Assert.AreEqual(1, nonRecursiveResult.Count);
            Assert.AreEqual("emp-1", nonRecursiveResult[0].Id);

            var recursiveResult = employees
                .Where(x => x.EmpDepts.Any(d => d.HeriarchyId.Contains("|dept-a|")))
                .ToList();
            Assert.AreEqual(1, recursiveResult.Count);
            Assert.AreEqual("emp-1", recursiveResult[0].Id);
        }

        [TestMethod]
        public void EmpDepts_LinqExpression_RecursiveWithDescendants()
        {
            var employees = new List<Employee>
            {
                new Employee
                {
                    Id = "emp-1",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-root", HeriarchyId = "|dept-root|", Name = "Root" },
                        new EmpDept { Id = "dept-child", HeriarchyId = "|dept-root|dept-child|", Name = "Child" }
                    }
                },
                new Employee
                {
                    Id = "emp-2",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-child", HeriarchyId = "|dept-root|dept-child|", Name = "Child" }
                    }
                },
                new Employee
                {
                    Id = "emp-3",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-other", HeriarchyId = "|dept-other|", Name = "Other" }
                    }
                }
            }.AsQueryable();

            var recursiveResult = employees
                .Where(x => x.EmpDepts.Any(d => d.HeriarchyId.Contains("|dept-root|")))
                .ToList();
            Assert.AreEqual(2, recursiveResult.Count);
            CollectionAssert.Contains(recursiveResult.Select(x => x.Id).ToList(), "emp-1");
            CollectionAssert.Contains(recursiveResult.Select(x => x.Id).ToList(), "emp-2");
        }

        [TestMethod]
        public void EmpDepts_LinqExpression_ContainsWithPipeChar()
        {
            var employees = new List<Employee>
            {
                new Employee
                {
                    Id = "emp-1",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-a", HeriarchyId = "|parent|dept-a|", Name = "Dept A" }
                    }
                },
                new Employee
                {
                    Id = "emp-2",
                    CorpId = "corp-1",
                    EmpDepts = new List<EmpDept>
                    {
                        new EmpDept { Id = "dept-b", HeriarchyId = "|parent|other|dept-b|", Name = "Dept B" }
                    }
                }
            }.AsQueryable();

            var result = employees
                .Where(x => x.EmpDepts.Any(d => d.HeriarchyId.Contains("|dept-a|")))
                .ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("emp-1", result[0].Id);

            var parentResult = employees
                .Where(x => x.EmpDepts.Any(d => d.HeriarchyId.Contains("|parent|")))
                .ToList();
            Assert.AreEqual(2, parentResult.Count);
        }
    }
}
