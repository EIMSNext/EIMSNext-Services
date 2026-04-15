using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core.Query;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Repositories;
using HKH.CSV;
using HKH.Mef2.Integration;
using MongoDB.Driver;
using NPOI.SS.UserModel;

namespace EIMSNext.Async.Tasks.Export
{
    public abstract class ExportProcessorBase : IExportProcessor
    {
        public abstract string Id { get; }

        public abstract Task<ExportFileBuilder.ExportFileResult> ExportAsync(
            EIMSNext.Service.Entities.ExportLog exportLog,
            IResolver resolver,
            CancellationToken ct);

        protected static async Task<ExportFileBuilder.ExportFileResult> ExportCsvByBatchAsync<TEntity>(
            string fileName,
            List<ExportColumn> columns,
            FilterDefinition<TEntity> filter,
            IResolver resolver,
            CancellationToken ct,
            int batchSize,
            Action<CSVWriter, List<ExportColumn>, IEnumerable<TEntity>> writeRows)
            where TEntity : EntityBase
        {
            var tempFile = Path.GetTempFileName();
            long totalCount = 0;

            try
            {
                await using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = ExportFileBuilder.CreateCsvWriter(stream);
                ExportFileBuilder.WriteCsvHeader(writer, columns);

                var repo = resolver.Resolve<IRepository<TEntity>>();
                long? lastCreateTime = null;
                string? lastId = null;

                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, repo.FilterBuilder, lastCreateTime, lastId);
                    var rows = await repo.Find(new MongoFindOptions<TEntity>
                    {
                        Filter = batchFilter,
                        Sort = repo.SortBuilder.Descending(x => x.CreateTime).Descending(x => x.Id),
                        Take = batchSize,
                    }).ToListAsync(ct);

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
                return new ExportFileBuilder.ExportFileResult
                {
                    FileName = fileName,
                    Content = await File.ReadAllBytesAsync(tempFile, ct),
                    TotalCount = totalCount,
                };
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        protected static async Task<ExportFileBuilder.ExportFileResult> ExportExcelByBatchAsync<TEntity>(
            string fileName,
            string sheetName,
            List<ExportColumn> columns,
            FilterDefinition<TEntity> filter,
            IResolver resolver,
            CancellationToken ct,
            int batchSize,
            Func<ISheet, ExportFileBuilder.ExcelStyles, List<ExportColumn>, IEnumerable<TEntity>, int, int> writeRows)
            where TEntity : EntityBase
        {
            var tempFile = Path.GetTempFileName();
            long totalCount = 0;

            try
            {
                using var workbook = ExportFileBuilder.CreateWorkbook();
                var sheet = ExportFileBuilder.InitializeExcelSheet(workbook, sheetName, columns, out var styles);
                var repo = resolver.Resolve<IRepository<TEntity>>();
                var rowIndex = 1;
                long? lastCreateTime = null;
                string? lastId = null;

                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, repo.FilterBuilder, lastCreateTime, lastId);
                    var rows = await repo.Find(new MongoFindOptions<TEntity>
                    {
                        Filter = batchFilter,
                        Sort = repo.SortBuilder.Descending(x => x.CreateTime).Descending(x => x.Id),
                        Take = batchSize,
                    }).ToListAsync(ct);

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
                workbook.Dispose();
                await stream.FlushAsync(ct);

                return new ExportFileBuilder.ExportFileResult
                {
                    FileName = fileName,
                    Content = await File.ReadAllBytesAsync(tempFile, ct),
                    TotalCount = totalCount,
                };
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        internal static FilterDefinition<T> BuildSeekFilter<T>(
            FilterDefinition<T> baseFilter,
            FilterDefinitionBuilder<T> builder,
            long? lastCreateTime,
            string? lastId)
            where T : EntityBase
        {
            if (!lastCreateTime.HasValue || string.IsNullOrWhiteSpace(lastId))
            {
                return baseFilter;
            }

            var seekFilter = builder.Or(
                builder.Lt(x => x.CreateTime, lastCreateTime.Value),
                builder.And(
                    builder.Eq(x => x.CreateTime, lastCreateTime.Value),
                    builder.Lt(x => x.Id, lastId)));

            return builder.And(baseFilter, seekFilter);
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
