using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace EIMSNext.Auth.Services
{
    public class AuditLoginService : IAuditLoginService
    {
        private readonly IAuthDbContext _dbContext;
        private readonly AuditLoginQueue _queue;
        private readonly ILogger<AuditLoginService> _logger;

        public AuditLoginService(
            IAuthDbContext dbContext,
            AuditLoginQueue queue,
            ILogger<AuditLoginService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _queue = queue;
            _logger = logger;
        }

        public Task AddAuditLogin(AuditLogin entity)
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
            return _dbContext.AddAuditLogin(entity);
        }
    }
}
