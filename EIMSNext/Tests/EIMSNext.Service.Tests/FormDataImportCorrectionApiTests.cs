using System.Composition.Hosting;
using System.Dynamic;
using System.Linq.Expressions;
using System.Text.Json;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormDataImportCorrectionApiTests
    {
        private const string CorpId = "corp-import-fix";
        private const string FormId = "form-import-fix";
        private const string AppId = "app-import-fix";

        [TestMethod]
        public async Task RetryImportAsync_AddOnly_IgnoresSubmittedDataIdAndAddsNewData()
        {
            var formDataService = new FakeFormDataService();
            formDataService.Seed(new FormData
            {
                Id = "existing-1",
                CorpId = CorpId,
                AppId = AppId,
                FormId = FormId,
                Data = ToExpando(new Dictionary<string, object?> { ["name"] = "old" }),
            });

            var importLog = NewImportLog(FormDataImportMode.AddOnly);
            importLog.AddCount = 3;
            importLog.FailedCount = 1;
            importLog.TotalCount = 4;

            var importLogService = new FakeImportLogService(importLog);
            var api = CreateApiService(formDataService, importLogService, SeedFormDef(["name"]));

            var response = await api.RetryImportAsync(importLog.Id, new FormDataImportRetryRequest
            {
                Rows =
                [
                    new FormDataImportCorrectionRow
                    {
                        DataId = "existing-1",
                        Data = ToExpando(new Dictionary<string, object?> { ["name"] = "new-row" }),
                    }
                ],
            });

            Assert.AreEqual(1L, response.AddCount);
            Assert.AreEqual(0L, response.UpdateCount);
            Assert.AreEqual(0L, response.FailedCount);
            Assert.AreEqual(0, response.Rows.Count);
            Assert.AreEqual(2, formDataService.Items.Count);
            Assert.AreEqual(1, formDataService.AddedEntities.Count);
            Assert.AreEqual(0, formDataService.ReplacedEntities.Count);
            Assert.AreEqual("old", ((IDictionary<string, object?>)formDataService.Items["existing-1"].Data)["name"]);
            Assert.IsNotNull(importLogService.LastCorrectionResult);
            Assert.AreEqual(4L, importLogService.LastCorrectionResult!.AddCount);
            Assert.AreEqual(0L, importLogService.LastCorrectionResult.UpdateCount);
            Assert.AreEqual(0L, importLogService.LastCorrectionResult.FailedCount);
            Assert.AreEqual(4L, importLogService.LastCorrectionResult.TotalCount);
        }

        [TestMethod]
        public async Task RetryImportAsync_DoesNotDependOnStoredEditableRowCountOrOrder()
        {
            var formDataService = new FakeFormDataService();
            formDataService.Seed(new FormData
            {
                Id = "data-a",
                CorpId = CorpId,
                AppId = AppId,
                FormId = FormId,
                Data = ToExpando(new Dictionary<string, object?> { ["code"] = "A", ["name"] = "before-a" }),
            });
            formDataService.Seed(new FormData
            {
                Id = "data-b",
                CorpId = CorpId,
                AppId = AppId,
                FormId = FormId,
                Data = ToExpando(new Dictionary<string, object?> { ["code"] = "B", ["name"] = "before-b" }),
            });

            var importLog = NewImportLog(FormDataImportMode.Upsert, matchField: "code");
            importLog.UpdateCount = 5;
            importLog.FailedCount = 3;
            importLog.TotalCount = 8;
            importLog.EditableErrorRowsJson =
                new List<FormDataImportEditableErrorRow>
                {
                    new() { RecordIndex = 0, StartRowNumber = 10, DataId = "data-a", Data = ToExpando(new Dictionary<string, object?> { ["code"] = "A" }) },
                }.SerializeToJson();
            importLog.EditableErrorRowCount = 1;

            var importLogService = new FakeImportLogService(importLog);
            var api = CreateApiService(formDataService, importLogService, SeedFormDef(["code", "name"]));

            var response = await api.RetryImportAsync(importLog.Id, new FormDataImportRetryRequest
            {
                Rows =
                [
                    new FormDataImportCorrectionRow
                    {
                        DataId = "data-b",
                        Data = ToExpando(new Dictionary<string, object?> { ["code"] = "B", ["name"] = "after-b" }),
                    },
                    new FormDataImportCorrectionRow
                    {
                        DataId = "data-a",
                        Data = ToExpando(new Dictionary<string, object?> { ["code"] = "A", ["name"] = "after-a" }),
                    }
                ],
            });

            Assert.AreEqual(0L, response.AddCount);
            Assert.AreEqual(2L, response.UpdateCount);
            Assert.AreEqual(0L, response.FailedCount);
            Assert.AreEqual(0, response.Rows.Count);
            Assert.AreEqual("after-a", ((IDictionary<string, object?>)formDataService.Items["data-a"].Data)["name"]);
            Assert.AreEqual("after-b", ((IDictionary<string, object?>)formDataService.Items["data-b"].Data)["name"]);
            Assert.IsNotNull(importLogService.LastCorrectionResult);
            Assert.AreEqual(0, importLogService.LastCorrectionResult!.EditableErrorRowCount);
            Assert.AreEqual(7L, importLogService.LastCorrectionResult.UpdateCount);
            Assert.AreEqual(1L, importLogService.LastCorrectionResult.FailedCount);
            Assert.AreEqual(8L, importLogService.LastCorrectionResult.TotalCount);
        }

        [TestMethod]
        public async Task RetryImportAsync_WhenStillInvalid_ReturnsErrorsAndKeepsCompletedWithErrors()
        {
            var formDataService = new FakeFormDataService();
            var importLog = NewImportLog(FormDataImportMode.AddOnly);
            importLog.TriggerValidation = true;
            importLog.TotalCount = 2;
            importLog.FailedCount = 2;

            var formDef = SeedFormDef(["name"]);
            formDef.Content.Items![0].Required = true;
            var importLogService = new FakeImportLogService(importLog);
            var api = CreateApiService(formDataService, importLogService, formDef);

            var response = await api.RetryImportAsync(importLog.Id, new FormDataImportRetryRequest
            {
                Rows =
                [
                    new FormDataImportCorrectionRow
                    {
                        Data = ToExpando(new Dictionary<string, object?> { ["name"] = "" }),
                    }
                ],
            });

            Assert.AreEqual(0L, response.AddCount);
            Assert.AreEqual(0L, response.UpdateCount);
            Assert.AreEqual(1L, response.FailedCount);
            Assert.AreEqual(1, response.Rows.Count);
            Assert.AreEqual("name", response.Rows[0].Errors[0].Field);
            Assert.AreEqual(FormDataImportStatus.CompletedWithErrors, importLog.Status);
            Assert.IsNotNull(importLogService.LastCorrectionResult);
            Assert.AreEqual(2L, importLogService.LastCorrectionResult!.FailedCount);
            Assert.AreEqual(1, importLogService.LastCorrectionResult.EditableErrorRowCount);
            StringAssert.Contains(importLogService.LastCorrectionResult.EditableErrorRowsJson ?? string.Empty, "name");
        }

        private static FormDataApiService CreateApiService(
            FakeFormDataService formDataService,
            FakeImportLogService importLogService,
            FormDef formDef)
        {
            var formDefService = new FakeFormDefService(formDef);
            var changeLogService = new FakeFormDataChangeLogService();
            var services = new Dictionary<Type, object>
            {
                [typeof(IFormDataService)] = formDataService,
                [typeof(IService<FormData>)] = formDataService,
                [typeof(IFormDefService)] = formDefService,
                [typeof(IService<FormDef>)] = formDefService,
                [typeof(IFormDataImportLogService)] = importLogService,
                [typeof(IService<FormDataImportLog>)] = importLogService,
                [typeof(IFormDataChangeLogService)] = changeLogService,
                [typeof(IService<FormDataChangeLog>)] = changeLogService,
                [typeof(IIdentityContext)] = new FakeIdentityContext(),
                [typeof(IServiceContext)] = new FakeServiceContext(),
                [typeof(ICacheClient)] = new FakeCacheClient(),
                [typeof(IMemoryCache)] = new MemoryCache(new MemoryCacheOptions()),
            };

            var resolver = new TestResolver(services);
            services[typeof(AdminPermissionEvaluator)] = new AdminPermissionEvaluator(resolver);
            return new FormDataApiService(resolver);
        }

        private static FormDataImportLog NewImportLog(FormDataImportMode mode, string? matchField = null)
        {
            return new FormDataImportLog
            {
                Id = "import-log-1",
                CorpId = CorpId,
                AppId = AppId,
                FormId = FormId,
                Status = FormDataImportStatus.CompletedWithErrors,
                Mode = mode,
                MatchField = matchField,
                TriggerValidation = false,
                ImportAction = DataAction.Save,
                EditableErrorRowsJson = "[]",
                EditableErrorRowCount = 1,
                MappingJson = new List<FormDataImportMappingItem>
                {
                    new() { ColumnIndex = 0, Field = "code", FieldTitle = "Code", FieldType = FieldType.Input },
                    new() { ColumnIndex = 1, Field = "name", FieldTitle = "Name", FieldType = FieldType.Input },
                }.SerializeToJson(),
                FieldSnapshotJson = new List<object>().SerializeToJson(),
            };
        }

        private static FormDef SeedFormDef(IEnumerable<string> fields)
        {
            return new FormDef
            {
                Id = FormId,
                CorpId = CorpId,
                AppId = AppId,
                Name = "Import Form",
                Content = new FormContent
                {
                    Items = fields.Select(field => new FieldDef
                    {
                        Field = field,
                        Title = field,
                        Type = FieldType.Input,
                        Props = new FieldProp(),
                    }).ToList(),
                },
            };
        }

        private static ExpandoObject ToExpando(IDictionary<string, object?> source)
        {
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            foreach (var (key, value) in source)
            {
                dict[key] = value;
            }

            return expando;
        }

        private sealed class TestResolver(IReadOnlyDictionary<Type, object> services) : IResolver
        {
            public CompositionContainer MefContainer => throw new NotSupportedException();
            public object Resolve(Type type, string? name = null) => services[type];
            public T Resolve<T>(string? name = null) where T : class => (T)services[typeof(T)];
            public T GetExport<T>(string? name = null) where T : class => Resolve<T>(name);
            public object GetExport(Type type, string? name = null) => Resolve(type, name);
            public IEnumerable<T> GetExports<T>(string? name = null) where T : class => [Resolve<T>(name)];
            public IEnumerable<object> GetExports(Type type, string? name = null) => [Resolve(type, name)];
        }

        private sealed class FakeIdentityContext : IIdentityContext
        {
            public string CurrentUserID => "user";
            public IUser? CurrentUser => null;
            public IEmployee? CurrentEmployee => null;
            public IdentityType IdentityType => IdentityType.CorpAdmin;
            public PublicScope PublicScope => PublicScope.None;
            public AccessControlLevel AccessControlLevel { get; set; } = AccessControlLevel.Allow;
            public string CurrentCorpId => CorpId;
            public string CurrentDashboardId => string.Empty;
            public string AccessToken => string.Empty;
        }

        private sealed class FakeServiceContext : IServiceContext
        {
            public string AccessToken { get; set; } = string.Empty;
            public string CorpId { get; set; } = FormDataImportCorrectionApiTests.CorpId;
            public Operator? Operator { get; set; }
            public string UserId { get; set; } = string.Empty;
            public IUser? User { get; set; }
            public IEmployee? Employee { get; set; }
            public string? ClientIp { get; set; }
            public DataAction Action { get; set; }
            public IScopeCache ScopeCache => throw new NotSupportedException();
            public T? UserAs<T>() where T : class, IUser => User as T;
        }

        private sealed class FakeCacheClient : ICacheClient
        {
            public string? GetString(string key, CacheScope scope, string scopeId = "") => null;
            public Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "") => Task.FromResult<string?>(null);
            public T? Get<T>(string key, CacheScope scope, string scopeId = "") => default;
            public Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "") => Task.FromResult<T?>(default);
            public void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) { }
            public Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) => Task.CompletedTask;
            public void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) { }
            public Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) => Task.CompletedTask;
            public void Refresh(string key, CacheScope scope, string scopeId = "") { }
            public Task RefreshAsync(string key, CacheScope scope, string scopeId = "") => Task.CompletedTask;
            public void Remove(string key, CacheScope scope, string scopeId = "") { }
            public Task RemoveAsync(string key, CacheScope scope, string scopeId = "") => Task.CompletedTask;
            public long Increment(string key, long delta, TimeSpan ttl, CacheScope scope, string scopeId = "") => 0;
            public Task<long> IncrementAsync(string key, long delta, TimeSpan ttl, CacheScope scope, string scopeId = "") => Task.FromResult(0L);
        }

        private sealed class FakeFormDefService(FormDef formDef) : FakeEntityService<FormDef>, IFormDefService
        {
            public override FormDef? Get(string id) => id == formDef.Id ? formDef : null;
            public override IQueryable<FormDef> All() => new[] { formDef }.AsQueryable();
        }

        private sealed class FakeFormDataChangeLogService : FakeEntityService<FormDataChangeLog>, IFormDataChangeLogService
        {
        }

        private sealed class FakeImportLogService(FormDataImportLog importLog) : FakeEntityService<FormDataImportLog>, IFormDataImportLogService
        {
            public CorrectionResultCall? LastCorrectionResult { get; private set; }

            public override FormDataImportLog? Get(string id) => id == importLog.Id ? importLog : null;

            public Task<bool> TryMarkProcessingAsync(string id, int retryCount) => throw new NotSupportedException();
            public Task MarkProcessingAsync(string id, long totalCount) => throw new NotSupportedException();
            public Task UpdateProgressAsync(string id, long processedCount, long addCount, long updateCount, long failedCount) => throw new NotSupportedException();
            public Task MarkSucceededAsync(string id, long totalCount, long addCount, long updateCount) => throw new NotSupportedException();
            public Task MarkCompletedWithErrorsAsync(string id, long totalCount, long addCount, long updateCount, long failedCount, string errorReportFileName, string errorReportObjectKey, string errorReportDownloadUrl, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount) => throw new NotSupportedException();
            public Task MarkFailedAsync(string id, string errorMessage, string? errorReportFileName = null, string? errorReportObjectKey = null, string? errorReportDownloadUrl = null) => throw new NotSupportedException();

            public Task MarkCorrectionResultAsync(string id, long totalCount, long addCount, long updateCount, long failedCount, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount)
            {
                LastCorrectionResult = new CorrectionResultCall(totalCount, addCount, updateCount, failedCount, editableErrorRowsJson, editableErrorRowsObjectKey, editableErrorRowCount);
                importLog.TotalCount = totalCount;
                importLog.ProcessedCount = totalCount;
                importLog.AddCount = addCount;
                importLog.UpdateCount = updateCount;
                importLog.FailedCount = failedCount;
                importLog.EditableErrorRowsJson = editableErrorRowsJson;
                importLog.EditableErrorRowsObjectKey = editableErrorRowsObjectKey;
                importLog.EditableErrorRowCount = editableErrorRowCount;
                importLog.Status = failedCount > 0 ? FormDataImportStatus.CompletedWithErrors : FormDataImportStatus.Succeeded;
                return Task.CompletedTask;
            }

            public Task UpdateEditableErrorsAsync(string id, string? editableErrorRowsJson, string? editableErrorRowsObjectKey, int editableErrorRowCount) => throw new NotSupportedException();
            public Task IncrementRetryAsync(string id) => throw new NotSupportedException();
        }

        private sealed class FakeFormDataService : FakeEntityService<FormData>, IFormDataService
        {
            public Dictionary<string, FormData> Items { get; } = new(StringComparer.Ordinal);
            public List<FormData> AddedEntities { get; } = [];
            public List<FormData> ReplacedEntities { get; } = [];

            public void Seed(FormData entity)
            {
                Items[entity.Id] = entity;
            }

            public override FormData? Get(string id) => Items.GetValueOrDefault(id);
            public override IQueryable<FormData> All() => Items.Values.AsQueryable();
            public override IFindFluent<FormData, FormData> Find(DynamicFindOptions<FormData> options)
            {
                var query = All();
                if (options.Filter != null && !options.Filter.IsEmpty)
                {
                    query = query.Where(item => Matches(item, options.Filter));
                }

                if (options.Skip > 0)
                {
                    query = query.Skip(options.Skip);
                }

                if (options.Take > 0)
                {
                    query = query.Take(options.Take);
                }

                return new FindFluentStub<FormData>(query);
            }
            public override Task AddAsync(FormData entity)
            {
                if (string.IsNullOrWhiteSpace(entity.Id))
                {
                    entity.Id = Guid.NewGuid().ToString("N");
                }

                Items[entity.Id] = entity;
                AddedEntities.Add(entity);
                return Task.CompletedTask;
            }

            public override Task<ReplaceOneResult> ReplaceAsync(FormData entity)
            {
                Items[entity.Id] = entity;
                ReplacedEntities.Add(entity);
                return Task.FromResult<ReplaceOneResult>(null!);
            }

            public void Add(IEnumerable<FormData> entities, IClientSessionHandle? session) => throw new NotSupportedException();
            public ReplaceOneResult Replace(FormData entity, IClientSessionHandle? session) => throw new NotSupportedException();
            public object Delete(IEnumerable<string> ids, IClientSessionHandle? session) => throw new NotSupportedException();
            public Task RestoreAsync(IEnumerable<string> ids) => throw new NotSupportedException();
            public Task PurgeAsync(IEnumerable<string> ids) => throw new NotSupportedException();
            public Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, CascadeMode cascade, string? eventIds) => throw new NotSupportedException();
            public Task<FilterOptionResult> GetFieldOptionsAsync(FilterOptionQuery query) => throw new NotSupportedException();

            private static bool Matches(FormData item, DynamicFilter filter)
            {
                if (filter.IsEmpty)
                {
                    return true;
                }

                if (filter.IsGroup || filter.Items?.Count > 0)
                {
                    var children = filter.Items ?? [];
                    return string.Equals(filter.Rel, FilterRel.Or, StringComparison.OrdinalIgnoreCase)
                        ? children.Any(child => Matches(item, child))
                        : children.All(child => Matches(item, child));
                }

                if (!string.Equals(filter.Op, FilterOp.Eq, StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException($"Unsupported filter op: {filter.Op}");
                }

                return ResolveFieldValue(item, filter.Field) switch
                {
                    null => filter.Value == null,
                    var value => string.Equals(value.ToString(), filter.Value?.ToString(), StringComparison.OrdinalIgnoreCase),
                };
            }

            private static object? ResolveFieldValue(FormData item, string? field)
            {
                return field switch
                {
                    Fields.Id or Fields.BsonId or "_id" => item.Id,
                    Fields.CorpId => item.CorpId,
                    Fields.FormId => item.FormId,
                    Fields.DeleteFlag => item.DeleteFlag,
                    _ when !string.IsNullOrWhiteSpace(field) && field.StartsWith($"{Fields.Data}.", StringComparison.OrdinalIgnoreCase)
                        => ResolveDataField(item.Data, field.Substring($"{Fields.Data}.".Length)),
                    _ => null,
                };
            }

            private static object? ResolveDataField(ExpandoObject data, string field)
            {
                var dict = (IDictionary<string, object?>)data;
                return dict.TryGetValue(field, out var value) ? value : null;
            }
        }

        private class FakeEntityService<T> : IService<T> where T : class, IMongoEntity
        {
            public virtual IMongoCollection<T> Collection => throw new NotSupportedException();
            public virtual T? Get(string id) => throw new NotSupportedException();
            public virtual IQueryable<T> All() => throw new NotSupportedException();
            public virtual IQueryable<T> Query(Expression<Func<T, bool>> where) => All().Where(where);
            public virtual IFindFluent<T, T> Find(DynamicFindOptions<T> options) => throw new NotSupportedException();
            public virtual IFindFluent<T, T> Find(Expression<Func<T, bool>> filter) => throw new NotSupportedException();
            public virtual long Count(DynamicFilter filter) => throw new NotSupportedException();
            public virtual long Count(Expression<Func<T, bool>> filter) => All().LongCount(filter);
            public virtual bool Exists(Expression<Func<T, bool>> where) => All().Any(where);
            public virtual bool Exists(DynamicFilter where) => throw new NotSupportedException();
            public virtual void Add(T entity) => throw new NotSupportedException();
            public virtual void Add(IEnumerable<T> entities) => throw new NotSupportedException();
            public virtual ReplaceOneResult Replace(T entity) => throw new NotSupportedException();
            public virtual object Delete(string id) => throw new NotSupportedException();
            public virtual object Delete(IEnumerable<string> ids) => throw new NotSupportedException();
            public virtual object Delete(DynamicFilter filter) => throw new NotSupportedException();
            public virtual Task<T?> GetAsync(string id) => Task.FromResult(Get(id));
            public virtual Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options) => throw new NotSupportedException();
            public virtual Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter) => throw new NotSupportedException();
            public virtual Task<long> CountAsync(DynamicFilter filter) => throw new NotSupportedException();
            public virtual Task<long> CountAsync(Expression<Func<T, bool>> filter) => Task.FromResult(Count(filter));
            public virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> where) => Task.FromResult(Exists(where));
            public virtual Task<bool> ExistsAsync(DynamicFilter where) => throw new NotSupportedException();
            public virtual Task AddAsync(T entity) => throw new NotSupportedException();
            public virtual Task AddAsync(IEnumerable<T> entities) => throw new NotSupportedException();
            public virtual Task<ReplaceOneResult> ReplaceAsync(T entity) => throw new NotSupportedException();
            public virtual Task<object> DeleteAsync(string id) => throw new NotSupportedException();
            public virtual Task<object> DeleteAsync(IEnumerable<string> ids) => throw new NotSupportedException();
            public virtual Task<object> DeleteAsync(DynamicFilter filter) => throw new NotSupportedException();
        }

        private sealed class FindFluentStub<T>(IQueryable<T> data) : IFindFluent<T, T>
        {
            public FilterDefinition<T> Filter { get; set; } = Builders<T>.Filter.Empty;
            public FindOptions<T, T> Options { get; } = new();
            public IFindFluent<T, TNewProjection> As<TNewProjection>(IBsonSerializer<TNewProjection> resultSerializer) => throw new NotSupportedException();
            public long Count(CancellationToken cancellationToken = default) => data.LongCount();
            public Task<long> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Count(cancellationToken));
            public long CountDocuments(CancellationToken cancellationToken = default) => data.LongCount();
            public Task<long> CountDocumentsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CountDocuments(cancellationToken));
            public IFindFluent<T, T> Limit(int? limit)
            {
                data = limit.HasValue ? data.Take(limit.Value) : data;
                return this;
            }
            public IFindFluent<T, TNewProjection> Project<TNewProjection>(ProjectionDefinition<T, TNewProjection> projection) => throw new NotSupportedException();
            public IFindFluent<T, T> Skip(int? skip)
            {
                data = skip.HasValue ? data.Skip(skip.Value) : data;
                return this;
            }
            public IFindFluent<T, T> Sort(SortDefinition<T> sort) => this;
            public IAsyncCursor<T> ToCursor(CancellationToken cancellationToken = default) => new AsyncCursorStub<T>(data);
            public Task<IAsyncCursor<T>> ToCursorAsync(CancellationToken cancellationToken = default) => Task.FromResult(ToCursor(cancellationToken));
            public string ToString(ExpressionTranslationOptions translationOptions) => ToString() ?? string.Empty;
        }

        private sealed class AsyncCursorStub<T>(IEnumerable<T> data) : IAsyncCursor<T>
        {
            private bool _moved;
            public IEnumerable<T> Current { get; private set; } = [];
            public void Dispose() { }
            public bool MoveNext(CancellationToken cancellationToken = default)
            {
                if (_moved)
                {
                    Current = [];
                    return false;
                }

                _moved = true;
                Current = data.ToList();
                return Current.Any();
            }
            public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));
        }

        private sealed record CorrectionResultCall(
            long TotalCount,
            long AddCount,
            long UpdateCount,
            long FailedCount,
            string? EditableErrorRowsJson,
            string? EditableErrorRowsObjectKey,
            int EditableErrorRowCount);
    }
}
