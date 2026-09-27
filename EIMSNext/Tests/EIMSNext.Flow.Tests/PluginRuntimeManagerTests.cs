using EIMSNext.Plugin.Runtime;
using EIMSNext.Plugin.Contracts;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class PluginRuntimeManagerTests
    {
        [TestMethod]
        public async Task Reload_Should_Select_Highest_Version()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var pluginV1Dir = Path.Combine(root, "Plugins", "sampleplugin", "1.0");
            var pluginV2Dir = Path.Combine(root, "Plugins", "sampleplugin", "2.0");
            Directory.CreateDirectory(pluginV1Dir);
            Directory.CreateDirectory(pluginV2Dir);
            System.IO.File.WriteAllText(Path.Combine(pluginV1Dir, "SamplePlugin.dll"), string.Empty);
            System.IO.File.WriteAllText(Path.Combine(pluginV2Dir, "SamplePlugin.dll"), string.Empty);

            var manager = new PluginRuntimeManager(
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<PluginRuntimeManager>.Instance,
                Path.Combine(root, "Plugins"),
                candidate => CreateFakeRuntime(candidate));

            var result = await manager.ReloadAsync();

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("2.0", manager.GetPlugin("sampleplugin")!.Version);
        }

        [TestMethod]
        public async Task Reload_Should_Replace_Previous_Runtime_With_Latest_Version()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var manager = new PluginRuntimeManager(
                services,
                NullLogger<PluginRuntimeManager>.Instance,
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Plugins"),
                candidate => CreateFakeRuntime(candidate));

            manager.SetActiveRuntimesForTest([
                CreateFakeRuntime(new PluginRuntimeManager.PluginAssemblyCandidate
                {
                    PluginId = "sampleplugin",
                    Version = ParseVersion("1.0"),
                    VersionText = "1.0",
                    AssemblyPath = Path.Combine("Plugins", "sampleplugin", "1.0", "SamplePlugin.dll")
                })
            ]);

            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var pluginV2Dir = Path.Combine(root, "Plugins", "sampleplugin", "2.0");
            Directory.CreateDirectory(pluginV2Dir);
            System.IO.File.WriteAllText(Path.Combine(pluginV2Dir, "SamplePlugin.dll"), string.Empty);

            manager = new PluginRuntimeManager(
                services,
                NullLogger<PluginRuntimeManager>.Instance,
                Path.Combine(root, "Plugins"),
                candidate => CreateFakeRuntime(candidate));
            manager.SetActiveRuntimesForTest([
                CreateFakeRuntime(new PluginRuntimeManager.PluginAssemblyCandidate
                {
                    PluginId = "sampleplugin",
                    Version = ParseVersion("1.0"),
                    VersionText = "1.0",
                    AssemblyPath = Path.Combine(root, "Plugins", "sampleplugin", "1.0", "SamplePlugin.dll")
                })
            ]);

            var result = await manager.ReloadAsync();

            Assert.AreEqual("2.0", result.Items[0].CurrentVersion);
            Assert.AreEqual("1.0", result.Items[0].PreviousVersion);
            Assert.AreEqual("2.0", manager.GetPlugin("sampleplugin")!.Version);
        }

        private static PluginRuntimeManager.PluginRuntime CreateFakeRuntime(PluginRuntimeManager.PluginAssemblyCandidate candidate)
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var runtimeType = typeof(PluginRuntimeManager).GetNestedType("PluginRuntime", System.Reflection.BindingFlags.NonPublic)!;
            var fakeDesc = new PluginDesc { Id = candidate.PluginId, Name = candidate.PluginId, Version = candidate.VersionText };
            var fakeLoadContextType = typeof(PluginRuntimeManager).GetNestedType("PluginLoadContext", System.Reflection.BindingFlags.NonPublic)!;
            var loadContext = Activator.CreateInstance(fakeLoadContextType, typeof(PluginRuntimeManagerTests).Assembly.Location)!;
            return (PluginRuntimeManager.PluginRuntime)Activator.CreateInstance(
                runtimeType,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                binder: null,
                args: [services, NullLogger.Instance, candidate.PluginId, candidate.Version, candidate.AssemblyPath, typeof(FakePlugin), fakeDesc, loadContext],
                culture: null)!;
        }

        private static PluginVersion ParseVersion(string text)
        {
            Assert.IsTrue(PluginVersion.TryParse(text, out var version), $"Failed to parse version '{text}' in test");
            return version;
        }

        private sealed class FakePlugin : IPlugin
        {
            public PluginDesc Description => new PluginDesc { Id = "fake", Name = "fake", Version = "1.0" };

            public void Dispose()
            {
            }

            public PluginExecResult Execute(PluginSetting setting, PluginExecArgs execArgs, PluginInvocationContext? context = null)
            {
                return new PluginExecResult();
            }
        }
    }
}
