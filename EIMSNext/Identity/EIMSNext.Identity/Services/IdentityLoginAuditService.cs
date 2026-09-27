using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

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
                entity.Id = ObjectId.GenerateNewId().ToString();
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
