using HKH.Mef2.Integration;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Persistence.PostgreSql;
using EIMSNext.Service.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EIMSNext.Service
{
    public class SerialNoSequenceService(IResolver resolver) : EntityServiceBase<SerialNoSequence>(resolver), ISerialNoSequenceService
    {
        public int NextFormSerialNo(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle)
        {
            return NextFormSerialNoInternal(corpId, appId, formId, key, cycle);
        }

        /// <summary>
        /// 表单级流水号计数(支持按日/月/年重置,key 用于同表单内多字段独立计数)
        /// </summary>
        private int NextFormSerialNoInternal(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle)
        {
            var anchor = GetCycleAnchor(SerialNoClock.Now, cycle);

            // 序列计数走独立短事务（自动提交，不进表单保存的业务事务）：
            // 行锁仅持有到本条 upsert 提交，不再被业务事务拖到 Commit 才释放——T2 并发塌陷的根因。
            // 语义取舍：号已分配但业务事务回滚时该号被消耗（产生空洞），与号段/重启场景一致。
            const string sql = """
                insert into "SerialNoSequence" as s
                    ("Id", "SerialNoType", "CorpId", "AppId", "FormId", "Key", "CurrDate", "CurrId",
                     "CreateTime", "UpdateTime", "DeleteFlag")
                values (@id, @serialNoType, @corpId, @appId, @formId, @key, @currDate, 1,
                        @now, @now, false)
                on conflict ("SerialNoType", "CorpId", "AppId", "FormId", "Key") do update
                    set "CurrId" = case when s."CurrDate" is distinct from excluded."CurrDate" then 1
                                        else s."CurrId" + 1 end,
                        "CurrDate" = excluded."CurrDate",
                        "UpdateTime" = excluded."UpdateTime"
                returning "CurrId"
                """;

            var id = Repository.NewId();
            var now = DateTime.UtcNow.ToTimeStampMs();
            // 取号连接走独立小连接池：与业务池隔离，最多占 8 个连接（每条约 1ms 的短事务），
            // 避免 100 并发时业务连接 + 取号连接叠加击穿 PG max_connections（53300）。
            SerialNoConnectionString ??= new NpgsqlConnectionStringBuilder(
                Repository.DbContext.Database.GetConnectionString())
            {
                ApplicationName = "EIMS.SerialNo",
                MaxPoolSize = 8
            }.ConnectionString;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var connection = new NpgsqlConnection(SerialNoConnectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    // 字符串参数显式标 citext，与 PostgreSqlCommandBuilder 行为保持一致
                    command.Parameters.AddWithValue("id", id).DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
                    command.Parameters.AddWithValue("serialNoType", (int)SerialNoType.Form);
                    command.Parameters.AddWithValue("corpId", corpId ?? string.Empty).DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
                    command.Parameters.AddWithValue("appId", appId ?? string.Empty).DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
                    command.Parameters.AddWithValue("formId", formId ?? string.Empty).DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
                    command.Parameters.AddWithValue("key", key ?? string.Empty).DataTypeName = EIMSNextModelConfiguration.CaseInsensitiveType;
                    command.Parameters.AddWithValue("currDate", anchor);
                    command.Parameters.AddWithValue("now", now);
                    var result = command.ExecuteScalar();
                    return result is null or DBNull ? 0 : Convert.ToInt32(result);
                }
                catch (PostgresException ex) when (IsRetryable(ex) && attempt < 4)
                {
                    // 40001 序列化失败 / 40P01 死锁：短暂退避后重试。
                    Thread.Sleep(10 * (attempt + 1));
                }
            }

            throw new InvalidOperationException("流水号生成冲突，请重试");
        }

        private static string? SerialNoConnectionString;

        private static bool IsRetryable(PostgresException exception)
        {
            return exception.SqlState is "40001" or "40P01";
        }

        /// <summary>
        /// 根据重置周期获取当前"桶"的时间锚点
        /// Never -> 任意非零(永不复位,使用 MinValue 不与正常日期匹配)
        /// Day   -> 当天 00:00:00
        /// Month -> 当月 1 号 00:00:00
        /// Year  -> 当年 1 月 1 号 00:00:00
        /// 锚点按本地日历日期派生；CurrDate 为 timestamptz，Npgsql 只接受 Utc Kind，
        /// 故保留 Utc（仅作分段标记，不代表真实瞬时）。
        /// </summary>
        private static DateTime GetCycleAnchor(DateTime localNow, SerialNoResetCycle cycle)
        {
            return cycle switch
            {
                SerialNoResetCycle.Never => DateTime.MinValue,
                SerialNoResetCycle.Day => new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Utc),
                SerialNoResetCycle.Month => new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                SerialNoResetCycle.Year => new DateTime(localNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => DateTime.MinValue
            };
        }
    }
}
