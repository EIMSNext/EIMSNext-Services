using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Services
{
    public class AuditLoginService : IAuditLoginService
    {
        private readonly IAuthDbContext _dbContext;
        private readonly ILogger<AuditLoginService> _logger;

        public AuditLoginService(IAuthDbContext dbContext, ILogger<AuditLoginService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger;
        }

        public Task AddAuditLogin(AuditLogin entity)
        {
            return _dbContext.AddAuditLogin(entity);
        }
    }
}
