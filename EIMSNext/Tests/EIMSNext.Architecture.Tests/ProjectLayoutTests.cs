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

        Assert.AreEqual(54, projectCount);
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
