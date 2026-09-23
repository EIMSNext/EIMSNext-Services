using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Identity.Services
{
    public class IdentityLoginAuditService : IIdentityLoginAuditService
    {
        private readonly IIdentityDbContext _dbContext;
        private readonly IdentityLoginAuditQueue _queue;
        private readonly ILogger<IdentityLoginAuditService> _logger;

        public IdentityLoginAuditService(
            IIdentityDbContext dbContext,
            IdentityLoginAuditQueue queue,
            ILogger<IdentityLoginAuditService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _queue = queue;
            _logger = logger;
        }

        public Task AddIdentityLoginAudit(IdentityLoginAudit entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                // PostgreSQL 侧 Id 保持 string 契约，统一产出 32 位无连字符 GUID，
                // 与 IRepository<T>.NewId() 的取值规则一致。
                entity.Id = Guid.NewGuid().ToString("N");
            }

            if (_queue.TryEnqueue(entity))
            {
                return Task.CompletedTask;
            }

            _logger.LogWarning(
                "Audit login queue is full; writing audit {AuditId} synchronously",
                entity.Id);
            return _dbContext.AddIdentityLoginAudit(entity);
        }
    }
}
