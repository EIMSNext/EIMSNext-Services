# EIMSNext.Tool.DbMaintenance

用于集中创建和维护 MongoDB 索引。

## 配置来源

- 当前目录下的 `appsettings.json`
- 当前目录下的 `appsettings.{Environment}.json`
- 环境变量
- 命令行参数

至少需要提供：

- `MongoDb:ConnectionString`
- `MongoDb:Database`

## 运行方式

```powershell
dotnet run --project EIMSNext-Services/EIMSNext/ApiHost/EIMSNext.Tool.DbMaintenance/EIMSNext.Tool.DbMaintenance.csproj
```

也可通过环境变量覆盖 Mongo 配置，例如：

```powershell
$env:MongoDb__ConnectionString = "mongodb://localhost:27017"
$env:MongoDb__Database = "EIMS"
dotnet run --project EIMSNext-Services/EIMSNext/ApiHost/EIMSNext.Tool.DbMaintenance/EIMSNext.Tool.DbMaintenance.csproj
```

## 约定

- 运行时项目不应在 `DbContext` 构造过程中自动创建索引。
- 新增或调整索引时，统一修改 `DbIndexManager.cs`。
