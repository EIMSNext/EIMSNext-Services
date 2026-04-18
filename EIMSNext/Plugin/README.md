# Plugin Layout

## Project Roles

- `EIMSNext.Plugin.Contracts`: plugin contracts and shared DTOs
- `EIMSNext.Plugin.Internal`: base classes for internal plugins, such as `InternalPluginBase`
- `EIMSNext.Plugin.Public`: public plugin-related library code, not concrete internal plugin implementations
- `SamplePlugin`: standalone sample plugin project that produces `SamplePlugin.dll`

## Sample Plugin Output

Build output:

```text
Plugin/SamplePlugin/bin/<Configuration>/net8.0/SamplePlugin.dll
```

Runtime deployment layout:

```text
<BaseDirectory>/Plugins/sampleplugin/1.0/SamplePlugin.dll
<BaseDirectory>/Plugins/sampleplugin/1.0/SamplePlugin.deps.json
```

Only files under `Plugins/` matching `*Plugin.dll` are scanned by the plugin runtime.

Shared assemblies such as `EIMSNext.Plugin.Contracts.dll` and `EIMSNext.Common.dll` are expected to come from the host output directory and default load context. They should not be copied into every plugin version directory.

`EIMSNext.Plugin.Internal` is an optional base library for internal plugins. If a plugin depends on it, the host must provide that assembly from its own output path instead of the plugin deployment folder.

## Reload Verification

1. Copy the built `SamplePlugin.dll` to `Plugins/sampleplugin/<version>/`
2. Start `EIMSNext.Service.Host`
3. Call `GET /api/v1/system/plugins`
4. Call `POST /api/v1/system/reloadplugin` after replacing or adding a higher version

The runtime always activates the highest available version for the same `pluginId`.
