using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Services;

namespace EIMSNext.Service.Contracts
{
	public interface IAuditLogService : IService<AuditLog>
	{
	}
}
