using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class AuditLogApiService(IResolver resolver) : ApiServiceBase<AuditLog, AuditLogViewModel,IAuditLogService>(resolver)
	{
		private static readonly Dictionary<string, ExportColumnType> AuditLogColumnTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			["operatorName"] = ExportColumnType.String,
			["createTime"] = ExportColumnType.Date,
			["action"] = ExportColumnType.String,
			["entityType"] = ExportColumnType.String,
			["detail"] = ExportColumnType.String,
			["clientIp"] = ExportColumnType.String,
		};

		public async Task<ExportResponse> ExportAsync(AuditLogExportRequest request)
		{
			ValidateAuditLogExportRequest(request);

			var totalCount = await CountExportAsync(request);
			var actualFormat = totalCount > 100000 ? ExportFormat.Csv : request.Format;
			var createBy = IdentityContext.CurrentEmployee?.Id ?? string.Empty;
			var columnsJson = request.Columns.SerializeToJson();
			var filterJson = request.SerializeToJson();
			var dedupKey = BuildDedupKey(new
			{
				ExportType = ExportType.AuditLog,
				request.Format,
				ActualFormat = actualFormat,
				request.Columns,
				request.EntityType,
				request.Action,
				request.OperatorName,
				request.StartTime,
				request.EndTime,
			});

			var exportLogService = Resolver.Resolve<IExportLogService>();
			var duplicated = await exportLogService.GetDuplicatedPendingAsync(IdentityContext.CurrentCorpId, createBy, dedupKey);
			if (duplicated != null)
			{
				return new ExportResponse
				{
					TaskId = duplicated.Id,
					IsDuplicate = true,
					ActualFormat = duplicated.ActualFormat,
					Message = "已有相同条件的导出任务正在处理中",
				};
			}

			var exportLog = new ExportLog
			{
				CorpId = IdentityContext.CurrentCorpId,
				ExportType = ExportType.AuditLog,
				RequestedFormat = request.Format,
				ActualFormat = actualFormat,
				Status = ExportLogStatus.Pending,
				ColumnsJson = columnsJson,
				FilterJson = filterJson,
				DedupKey = dedupKey,
				TotalCount = totalCount,
			};

			await exportLogService.AddAsync(exportLog);
			await Resolver.Resolve<IMessagePublisher>().PublishAsync(new ExportLogTaskArgs
			{
				ExportLogId = exportLog.Id,
				CorpId = exportLog.CorpId ?? string.Empty,
			});

			return new ExportResponse
			{
				TaskId = exportLog.Id,
				IsDuplicate = false,
				ActualFormat = actualFormat,
				Message = actualFormat != request.Format ? "超过 10W 行，已自动切换为 CSV 导出" : null,
			};
		}

		private async Task<long> CountExportAsync(AuditLogExportRequest request)
		{
			var filter = BuildAuditLogFilter(request);
			return await Resolver.GetRepository<AuditLog>().CountAsync(filter);
		}

		private FilterDefinition<AuditLog> BuildAuditLogFilter(AuditLogExportRequest request)
		{
			var builder = Builders<AuditLog>.Filter;
			var filters = new List<FilterDefinition<AuditLog>>
			{
				builder.Eq(x => x.CorpId, IdentityContext.CurrentCorpId),
				builder.Ne(x => x.DeleteFlag, true),
			};

			if (!string.IsNullOrWhiteSpace(request.EntityType))
			{
				filters.Add(builder.Eq(x => x.EntityType, request.EntityType));
			}

			if (!string.IsNullOrWhiteSpace(request.Action) && Enum.TryParse<DbAction>(request.Action, true, out var action))
			{
				filters.Add(builder.Eq(x => x.Action, action));
			}

			if (!string.IsNullOrWhiteSpace(request.OperatorName))
			{
				filters.Add(builder.Regex("CreateBy.Label", new MongoDB.Bson.BsonRegularExpression(request.OperatorName, "i")));
			}

			if (request.StartTime.HasValue)
			{
				filters.Add(builder.Gte(x => x.CreateTime, request.StartTime.Value));
			}

			if (request.EndTime.HasValue)
			{
				filters.Add(builder.Lte(x => x.CreateTime, request.EndTime.Value));
			}

			return filters.Count == 1 ? filters[0] : builder.And(filters);
		}

		private static void ValidateAuditLogExportRequest(AuditLogExportRequest request)
		{
			if (request.Columns == null || request.Columns.Count == 0)
			{
				throw new ArgumentException("导出列不能为空");
			}

			foreach (var column in request.Columns)
			{
				if (string.IsNullOrWhiteSpace(column.Key))
				{
					throw new ArgumentException("导出列标识不能为空");
				}

				if (string.IsNullOrWhiteSpace(column.Header))
				{
					throw new ArgumentException($"导出列标题不能为空: {column.Key}");
				}

				if (!AuditLogColumnTypes.TryGetValue(column.Key, out var type))
				{
					throw new ArgumentException($"不支持的导出列: {column.Key}");
				}

				column.Type = type;
			}

			request.Columns = request.Columns
				.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
				.Select(x => x.First())
				.ToList();

			if (!string.IsNullOrWhiteSpace(request.Action) && !Enum.TryParse<DbAction>(request.Action, true, out _))
			{
				throw new ArgumentException($"不支持的操作类型: {request.Action}");
			}
		}

		private static string BuildDedupKey(object source)
		{
			var json = source.SerializeToJson();
			var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
			return Convert.ToHexString(bytes);
		}
	}
}
