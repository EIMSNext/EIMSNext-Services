using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EIMSNext.Common.Extension;
using EIMSNext.Common.Serialization;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Serialization;

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace EIMSNext.Core.Test
{
    public class TestBase
    {
        protected DbContext? _dbContext;
        protected MongoTransactionScope? _scope;
        protected static bool _initialized = false;

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

            _dbContext = DbContext.Create();
            _scope = new MongoTransactionScope(_dbContext);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _scope?.AbortTransaction();
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

            opt.Converters.Add(new BsonDocumentJsonConverter());
            opt.Converters.Add(new ExceptionJsonConverter());
            opt.Converters.Add(new FlexibleEnumConverterFactory());
            opt.Converters.Add(new ObjectJsonConverter());
            opt.Converters.Add(new ExpandoObjectJsonConverter());

            JsonSerializerExtension.SetOptions(opt);
        }
    }
}
