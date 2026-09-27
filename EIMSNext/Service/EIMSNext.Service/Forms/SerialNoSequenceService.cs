using HKH.Mef2.Integration;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class SerialNoSequenceService(IResolver resolver) : EntityServiceBase<SerialNoSequence>(resolver), ISerialNoSequenceService
    {
        private static Dictionary<SerialNoType, string> defaultSNFormats = new Dictionary<SerialNoType, string> {
            {SerialNoType.Corporate, "{0:yyyyMMdd}{1:00}{2:0000}" },
            {SerialNoType.Form,"{0:yyyyMMdd}{1:0000}" }
        };

        public string NextCorpCode(PlatformType platform)
        {
            return NextSerialNo(new NextSerialNoParameter(SerialNoType.Corporate, platform, string.Empty, string.Empty, string.Empty));
        }

        public int NextFormSerialNo(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle)
        {
            return NextFormSerialNoInternal(corpId, appId, formId, key, cycle);
        }

        private string NextSerialNo(NextSerialNoParameter parameter)
        {
            if (parameter.SerialNoType == SerialNoType.Corporate)
            {
                var utcToday = UtcDay();
                var sequence = NextCorporateSerialNo(utcToday);
                var fmt = defaultSNFormats[SerialNoType.Corporate];
                return string.Format(fmt, utcToday, (int)parameter.Platform, sequence);
            }
            throw new NotSupportedException("Unknown SerialNoType");
        }

        private int NextCorporateSerialNo(DateTime utcToday)
        {
            var filter = Builders<SerialNoSequence>.Filter.Eq(x => x.SerialNoType, SerialNoType.Corporate);
            var resetFilter = Builders<SerialNoSequence>.Filter.And(
                filter,
                Builders<SerialNoSequence>.Filter.Ne(x => x.CurrDate, utcToday));
            var resetUpdate = Builders<SerialNoSequence>.Update
                .Set(x => x.CurrDate, utcToday)
                .Set(x => x.CurrId, 1);

            if (FindOneAndUpdate(resetFilter, resetUpdate, false, null) != null)
            {
                return 1;
            }

            var update = Builders<SerialNoSequence>.Update
                .SetOnInsert(x => x.Id, Repository.NewId())
                .SetOnInsert(x => x.SerialNoType, SerialNoType.Corporate)
                .SetOnInsert(x => x.CurrDate, utcToday)
                .Inc(x => x.CurrId, 1);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var current = FindOneAndUpdate(filter, update, true, null);
                    if (current != null) return current.CurrId ?? 1;
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Another request created the singleton; retry the atomic increment.
                }
                catch (MongoException ex) when (IsWriteConflict(ex) && attempt < 4)
                {
                    Thread.Sleep(10 * (attempt + 1));
                }
            }

            throw new InvalidOperationException("企业编号生成冲突，请重试");
        }

        private static DateTime UtcDay()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// 表单级流水号计数(支持按日/月/年重置,key 用于同表单内多字段独立计数)
        /// </summary>
        private int NextFormSerialNoInternal(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle)
        {
            var now = DateTime.UtcNow;
            var anchor = GetCycleAnchor(now, cycle);
            var filter = Builders<SerialNoSequence>.Filter.And(
                Builders<SerialNoSequence>.Filter.Eq(x => x.SerialNoType, SerialNoType.Form),
                Builders<SerialNoSequence>.Filter.Eq(x => x.CorpId, corpId),
                Builders<SerialNoSequence>.Filter.Eq(x => x.AppId, appId),
                Builders<SerialNoSequence>.Filter.Eq(x => x.FormId, formId),
                Builders<SerialNoSequence>.Filter.Eq(x => x.Key, key));
            // 序列计数是独立的原子计数器。不要加入提交事务，否则并发提交同一表单
            // 会在同一序列文档上产生 Mongo WriteConflict；业务事务回滚时允许出现号段间隙。
            IClientSessionHandle? session = null;

            if (cycle != SerialNoResetCycle.Never)
            {
                var resetFilter = Builders<SerialNoSequence>.Filter.And(
                    filter,
                    Builders<SerialNoSequence>.Filter.Ne(x => x.CurrDate, anchor));
                var resetUpdate = Builders<SerialNoSequence>.Update
                    .Set(x => x.CurrDate, anchor)
                    .Set(x => x.CurrId, 1);
                var resetResult = FindOneAndUpdate(resetFilter, resetUpdate, false, session);

                if (resetResult != null)
                {
                    return resetResult.CurrId ?? 1;
                }
            }

            var update = Builders<SerialNoSequence>.Update
                .SetOnInsert(x => x.Id, Repository.NewId())
                .SetOnInsert(x => x.SerialNoType, SerialNoType.Form)
                .SetOnInsert(x => x.CorpId, corpId)
                .SetOnInsert(x => x.AppId, appId)
                .SetOnInsert(x => x.FormId, formId)
                .SetOnInsert(x => x.Key, key)
                .SetOnInsert(x => x.CurrDate, anchor)
                .Inc(x => x.CurrId, 1);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var currentSerialNo = FindOneAndUpdate(filter, update, true, session);
                    if (currentSerialNo != null)
                    {
                        return currentSerialNo.CurrId ?? 1;
                    }
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    var currentSerialNo = FindOneAndUpdate(
                        filter,
                        Builders<SerialNoSequence>.Update.Inc(x => x.CurrId, 1),
                        false,
                        session);
                    if (currentSerialNo != null)
                    {
                        return currentSerialNo.CurrId ?? 1;
                    }
                }
                catch (MongoException ex) when (IsWriteConflict(ex) && attempt < 4)
                {
                    Thread.Sleep(10 * (attempt + 1));
                }
            }

            throw new InvalidOperationException("流水号生成冲突，请重试");
        }

        private static bool IsWriteConflict(MongoException exception)
        {
            return exception.Message.Contains("WriteConflict", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("Please retry your operation", StringComparison.OrdinalIgnoreCase)
                || exception is MongoCommandException command && command.Code == 112
                || exception is MongoWriteException write && write.WriteError?.Code == 112;
        }

        private SerialNoSequence? FindOneAndUpdate(
            FilterDefinition<SerialNoSequence> filter,
            UpdateDefinition<SerialNoSequence> update,
            bool isUpsert,
            IClientSessionHandle? session)
        {
            var options = new FindOneAndUpdateOptions<SerialNoSequence>
            {
                IsUpsert = isUpsert,
                ReturnDocument = ReturnDocument.After
            };

            return session == null
                ? Repository.Collection.FindOneAndUpdate(filter, update, options)
                : Repository.Collection.FindOneAndUpdate(session, filter, update, options);
        }

        /// <summary>
        /// 根据重置周期获取当前"桶"的时间锚点
        /// Never -> 任意非零(永不复位,使用 MinValue 不与正常日期匹配)
        /// Day   -> 当天 00:00:00
        /// Month -> 当月 1 号 00:00:00
        /// Year  -> 当年 1 月 1 号 00:00:00
        /// </summary>
        private static DateTime GetCycleAnchor(DateTime utcNow, SerialNoResetCycle cycle)
        {
            return cycle switch
            {
                SerialNoResetCycle.Never => DateTime.MinValue,
                SerialNoResetCycle.Day => new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc),
                SerialNoResetCycle.Month => new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                SerialNoResetCycle.Year => new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => DateTime.MinValue
            };
        }
    }
}
