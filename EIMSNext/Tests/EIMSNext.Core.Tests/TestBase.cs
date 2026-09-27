using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using EIMSNext.Core.Repositories;
using EIMSNext.Json.Serialization;
using EIMSNext.Persistence.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Tests
{
    /// <summary>
    /// 集成测试基类。
    /// </summary>
    public class TestBase
    {
        /// <summary>
        /// 测试自有实体的上下文。
        /// </summary>
        /// <remarks>
        /// 本基类的子类（<c>DynamicRepositoryTest</c> / <c>DynamicFindTest</c> / <c>EntityRepositoryTest</c>）
        /// 只操作测试自有实体 <c>FormData</c> / <c>EntityData</c>。它们不在生产
        /// <see cref="PostgreSqlDbContext"/> 的模型里，因此这里用
        /// <see cref="TestPostgreSqlDbContext"/>；与真实业务表相关的用例（事务、
        /// 表结构一致性）各自用 <see cref="PostgreSqlDbContext"/>。
        /// </remarks>
        protected TestPostgreSqlDbContext? _dbContext;
        protected TransactionScope? _scope;
        protected static bool _initialized = false;
        private static bool _schemaReady = false;

        public void InitOnce()
        {
            SetJsonOptions();
        }

        [TestInitialize]
        public void Init()
        {
            if (!_initialized)
            {
                InitOnce();
                _initialized = true;
            }

            // 测试表按模型现场重建一次即可，后续用例复用（每个用例都在事务里、结束即回滚）。
            if (!_schemaReady)
            {
                TestDbFactory.EnsureTestSchema();
                _schemaReady = true;
            }

            _dbContext = TestDbFactory.CreateTest();
            _scope = new TransactionScope(_dbContext);

            // 清空测试夹具用到的两张表。测试实体实现 IEntity → 带全局软删除过滤，
            // 这里要清全部行（含已软删除的），所以必须 IgnoreQueryFilters。
            _dbContext.TestFormDatas.IgnoreQueryFilters().ExecuteDelete();
            _dbContext.EntityDatas.IgnoreQueryFilters().ExecuteDelete();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _scope?.Dispose();
            _dbContext?.Dispose();
        }

        private void SetJsonOptions()
        {
            var opt = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            opt.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            opt.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            opt.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opt.PropertyNameCaseInsensitive = true;
            opt.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            opt.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            opt.Converters.Add(new ExceptionJsonConverter());
            opt.Converters.Add(new FlexibleEnumConverterFactory());
            opt.Converters.Add(new ObjectJsonConverter());
            // 两个程序集都提供 ExpandoObjectJsonConverter（Json 层通用版 / PostgreSql 层 jsonb 版），
            // 这里显式限定 Json 层版本：本方法是给业务 JSON 序列化全局注册转换器。
            opt.Converters.Add(new EIMSNext.Json.Serialization.ExpandoObjectJsonConverter());
            // 动态字段容器（FormData.Data）为 Dictionary<string, object?>，请求/往返路径需要同样的深度还原。
            opt.Converters.Add(new EIMSNext.Json.Serialization.DictionaryJsonConverter());

            JsonSerializerExtension.SetOptions(opt);
        }
    }
}
