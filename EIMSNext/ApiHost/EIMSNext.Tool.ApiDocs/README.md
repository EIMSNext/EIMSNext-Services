# EIMSNext API docs generator

The generator is intentionally source based. It scans public V1 request/query models, reads XML summaries, and removes properties marked with `JsonIgnore`, `IgnoreDataMember`, or an OData `Ignore(...)` configuration.

```powershell
dotnet run --project .\ApiHost\EIMSNext.Tool.ApiDocs\EIMSNext.Tool.ApiDocs.csproj -- `
  --source .\ --output D:\EIMS\Code\Home\apidocs\pages\api-models.js
```

The command exits with code 2 when a documented model property has no XML summary. Keep the generated file checked in with the static `file://` documentation site.
