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

        // 迁移至 PostgreSQL 后 MongoDB 持久化项目已移除，并新增共享测试桩项目
        // Tests\EIMSNext.TestSupport 与 easyun 分支的第三方集成登录客户端项目（DingTalk/Feishu/WeChat/WxWork 及其 Identity.* 变体，共 9 个）。
        Assert.AreEqual(64, projectCount);
    }

    /// <summary>
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
