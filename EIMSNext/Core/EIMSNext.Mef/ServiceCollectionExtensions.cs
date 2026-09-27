using System.Composition.Convention;
using System.Reflection;
using System.Runtime.Loader;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace EIMSNext.Mef;

public static class ServiceCollectionExtensions
{
    public static void AddGlobalMef(this IServiceCollection services, string directory, string searchPattern = "*.dll")
    {
        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories)
            .Where(path => !IsUnderPlugins(directory, path))
            .Where(path => Path.GetFileName(path).StartsWith("EIMSNext.", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).EndsWith("Plugin.dll", StringComparison.OrdinalIgnoreCase));

        var configuration = new DefaultContainerConfiguration();
        var conventions = new ConventionBuilder();
        conventions.ForTypesMatching(_ => true).Shared();
        configuration.WithAssemblies(files.Select(TryLoadComposableAssembly).Where(assembly => assembly != null)!, conventions);
        services.EnableMef2(configuration);
    }

    private static Assembly? TryLoadComposableAssembly(string assemblyPath)
    {
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            _ = assembly.DefinedTypes.Count();
            return assembly;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsUnderPlugins(string baseDirectory, string filePath)
    {
        var pluginRoot = Path.Combine(baseDirectory, "Plugins") + Path.DirectorySeparatorChar;
        return filePath.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase);
    }
}
