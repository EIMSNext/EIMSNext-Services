using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EIMSNext.Architecture.Tests;

[TestClass]
public sealed class ProjectLayoutTests
{
    [TestMethod]
    public void ServiceModules_AreOrganizedByBusinessBoundary()
    {
        var solutionRoot = FindSolutionRoot();
        Assert.IsTrue(Directory.Exists(Path.Combine(solutionRoot, "Service", "EIMSNext.Service", "Tenancy")));
        Assert.IsTrue(Directory.Exists(Path.Combine(solutionRoot, "Service", "EIMSNext.Service", "Studio")));
        Assert.IsTrue(Directory.Exists(Path.Combine(solutionRoot, "Service", "EIMSNext.Service", "Forms")));
    }

    [TestMethod]
    public void Solution_ContainsTheExpectedProjectCount()
    {
        var solutionRoot = FindSolutionRoot();
        var projectCount = Directory.GetFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Count(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase));

        // PostgreSQL 迁移：MongoDB 持久化项目已删除（原 54 个），
        // 并新增共享测试桩项目 Tests\EIMSNext.TestSupport（StubRepository<T> / StubEntityService<T>）。
        Assert.AreEqual(55, projectCount);
    }

    /// <summary>
    /// 迁移回归防护：确保已删除的 Mongo 持久化项目不会被重新引入，
    /// 且替换它的 PostgreSQL 持久化项目始终存在。
    /// </summary>
    [TestMethod]
    public void PersistenceProjects_UsePostgreSqlInsteadOfMongo()
    {
        var solutionRoot = FindSolutionRoot();
        Assert.IsTrue(
            Directory.Exists(Path.Combine(solutionRoot, "Core", "EIMSNext.Persistence.PostgreSql")),
            "PostgreSQL 持久化项目必须存在。");
        Assert.IsFalse(
            Directory.Exists(Path.Combine(solutionRoot, "Core", "EIMSNext.Persistence.Mongo")),
            "MongoDB 持久化项目已迁移至 PostgreSQL，不应再存在。");
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "EIMSNext.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("EIMSNext.sln was not found.");
    }
}
