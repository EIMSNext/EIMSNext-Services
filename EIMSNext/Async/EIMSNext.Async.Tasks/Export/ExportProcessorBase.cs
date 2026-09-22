using System.Linq.Expressions;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using HKH.CSV;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;

namespace EIMSNext.Async.Tasks.Export
{
    public abstract class ExportProcessorBase : IExportProcessor
    {
        public abstract Task<ExportFileBuilder.ExportFileResult> ExportAsync(
            EIMSNext.Entities.ExportLog exportLog,
            IResolver resolver,
            CancellationToken ct);

        protected static async Task<ExportFileBuilder.ExportFileResult> ExportCsvByBatchAsync<TEntity>(
            string fileName,
            List<ExportColumn> columns,
            Expression<Func<TEntity, bool>> filter,
            IResolver resolver,
            CancellationToken ct,
            int batchSize,
            Action<CSVWriter, List<ExportColumn>, IEnumerable<TEntity>> writeRows)
            where TEntity : EntityBase
        {
            var tempFile = CreateTempFilePath(fileName);
            long totalCount = 0;

            await using (var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using var writer = ExportFileBuilder.CreateCsvWriter(stream);
                ExportFileBuilder.WriteCsvHeader(writer, columns);

                var repo = resolver.Resolve<IRepository<TEntity>>();
                long? lastCreateTime = null;
                string? lastId = null;

                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, lastCreateTime, lastId);
                    var rows = await repo.Find(batchFilter)
                        .OrderByDescending(x => x.CreateTime)
                        .ThenByDescending(x => x.Id)
                        .Take(batchSize)
                        .ToListAsync(ct);

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    writeRows(writer, columns, rows);
                    writer.Flush();
                    totalCount += rows.Count;

                    var last = rows[^1];
                    lastCreateTime = last.CreateTime;
                    lastId = last.Id;
                }

                await stream.FlushAsync(ct);
            }

            return new ExportFileBuilder.ExportFileResult
            {
                FileName = fileName,
                Content = OpenTempFileForRead(tempFile),
                TotalCount = totalCount,
            };
        }

        protected static async Task<ExportFileBuilder.ExportFileResult> ExportExcelByBatchAsync<TEntity>(
            string fileName,
            List<ExportColumn> columns,
            Expression<Func<TEntity, bool>> filter,
            IResolver resolver,
            CancellationToken ct,
            int batchSize,
            Func<ISheet, ExportFileBuilder.ExcelStyles, List<ExportColumn>, IEnumerable<TEntity>, int, int> writeRows)
            where TEntity : EntityBase
        {
            var tempFile = CreateTempFilePath(fileName);
            long totalCount = 0;

            using (var workbook = ExportFileBuilder.CreateWorkbook())
            {
                var sheet = ExportFileBuilder.InitializeExcelSheet(workbook, "Sheet1", columns, out var styles);
                var repo = resolver.Resolve<IRepository<TEntity>>();
                var rowIndex = 1;
                long? lastCreateTime = null;
                string? lastId = null;

                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, lastCreateTime, lastId);
                    var rows = await repo.Find(batchFilter)
                        .OrderByDescending(x => x.CreateTime)
                        .ThenByDescending(x => x.Id)
                        .Take(batchSize)
                        .ToListAsync(ct);

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    rowIndex = writeRows(sheet, styles, columns, rows, rowIndex);
                    totalCount += rows.Count;

                    var last = rows[^1];
                    lastCreateTime = last.CreateTime;
                    lastId = last.Id;
                }

                await using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(stream, false);
            }

            return new ExportFileBuilder.ExportFileResult
            {
                FileName = fileName,
                Content = OpenTempFileForRead(tempFile),
                TotalCount = totalCount,
            };
        }

        internal static string CreateTempFilePath(string fileName)
        {
            var baseDirectory = string.IsNullOrWhiteSpace(EIMSNext.Common.Constants.BaseDirectory)
                ? AppContext.BaseDirectory
                : EIMSNext.Common.Constants.BaseDirectory;
            var tempDirectory = Path.Combine(baseDirectory, "temp", "export", DateTime.UtcNow.ToString("yyyyMMdd"));
            Directory.CreateDirectory(tempDirectory);
            var tempFileName = $"{SanitizeFileName(fileName)}";
            return Path.Combine(tempDirectory, tempFileName);
        }

        internal static FileStream OpenTempFileForRead(string tempFile)
        {
            return new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        }

        /// <summary>
        /// 在基础过滤条件上叠加 seek（keyset）分页条件。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="baseFilter">基础过滤谓词。</param>
        /// <param name="lastCreateTime">上一批最后一行的 CreateTime。</param>
        /// <param name="lastId">上一批最后一行的 Id。</param>
        /// <returns>叠加后的过滤谓词。</returns>
        /// <remarks>
        /// <para>
        /// 原实现用 <c>FilterDefinitionBuilder.Or/And/Lt/Eq</c> 构造 Mongo 过滤定义；
        /// EF Core 只能接受表达式树，因此这里改为用 <see cref="DynamicQueryExtensions.AndAlso{T}"/>
        /// 组合 <see cref="Expression{TDelegate}"/>。
        /// </para>
        /// <para>
        /// 语义与原实现完全一致：排序键为 <c>(CreateTime desc, Id desc)</c>，seek 条件是
        /// <c>CreateTime &lt; lastCreateTime OR (CreateTime == lastCreateTime AND Id &lt; lastId)</c>。
        /// 这是为了在导出大表时避免 <c>OFFSET</c> 逐页扫描——PostgreSQL 的 <c>OFFSET</c>
        /// 需要丢弃前面所有行，深分页会退化成 O(n²)。
        /// </para>
        /// </remarks>
        internal static Expression<Func<T, bool>> BuildSeekFilter<T>(
            Expression<Func<T, bool>> baseFilter,
            long? lastCreateTime,
            string? lastId)
            where T : EntityBase
        {
            if (!lastCreateTime.HasValue || string.IsNullOrWhiteSpace(lastId))
            {
                return baseFilter;
            }

            var createTime = lastCreateTime.Value;
            var id = lastId;
            Expression<Func<T, bool>> seek = x =>
                x.CreateTime < createTime || (x.CreateTime == createTime && string.Compare(x.Id, id) < 0);

            return baseFilter.AndAlso(seek);
        }

        protected static string SanitizeFileName(string? fileName)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? "export" : fileName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return string.IsNullOrWhiteSpace(name) ? "export" : name;
        }
    }
}
