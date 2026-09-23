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
            // 当日首先生成时把计数器重置为 1，否则自增。
            // 同一语句内完成「不存在则插入、存在则按条件重置或自增」，天然原子。
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
            return ExecuteScalarInt(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("id", id);
                cmd.Parameters.AddWithValue("serialNoType", (int)SerialNoType.Corporate);
                cmd.Parameters.AddWithValue("corpId", string.Empty);
                cmd.Parameters.AddWithValue("appId", string.Empty);
                cmd.Parameters.AddWithValue("formId", string.Empty);
                cmd.Parameters.AddWithValue("key", string.Empty);
                cmd.Parameters.AddWithValue("currDate", utcToday);
                cmd.Parameters.AddWithValue("now", now);
            });
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
            var anchor = GetCycleAnchor(DateTime.UtcNow, cycle);

            // 序列计数使用当前 PostgreSQL DbContext 的连接和事务，
            // 由 PostgreSqlCommandBuilder 绑定 CurrentTransaction，保证命令不会脱离业务事务。
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
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    return ExecuteScalarInt(sql, cmd =>
                    {
                        cmd.Parameters.AddWithValue("id", id);
                        cmd.Parameters.AddWithValue("serialNoType", (int)SerialNoType.Form);
                        cmd.Parameters.AddWithValue("corpId", corpId ?? string.Empty);
                        cmd.Parameters.AddWithValue("appId", appId ?? string.Empty);
                        cmd.Parameters.AddWithValue("formId", formId ?? string.Empty);
                        cmd.Parameters.AddWithValue("key", key ?? string.Empty);
                        cmd.Parameters.AddWithValue("currDate", anchor);
                        cmd.Parameters.AddWithValue("now", now);
                    });
                }
                catch (PostgresException ex) when (IsRetryable(ex) && attempt < 4)
                {
                    // 40001 序列化失败 / 40P01 死锁：短暂退避后重试。
                    Thread.Sleep(10 * (attempt + 1));
                }
            }

            throw new InvalidOperationException("流水号生成冲突，请重试");
        }

        /// <summary>
        /// 在当前 DbContext 连接上执行返回单个整数的计数语句。
        /// </summary>
        private int ExecuteScalarInt(string sql, Action<NpgsqlCommand> bind)
        {
            using var command = PostgreSqlCommandBuilder.Create(Repository.DbContext, sql);
            bind(command);
            var result = command.ExecuteScalar();
            return result is null or DBNull ? 0 : Convert.ToInt32(result);
        }

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
