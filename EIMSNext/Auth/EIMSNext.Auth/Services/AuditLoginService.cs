using EIMSNext.Auth.Entity;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Services
{
    public class AuditLoginService : IAuditLoginService
    {
        private readonly IAuthDbContext _context;
        private readonly ILogger<AuditLoginService> _logger;

        public AuditLoginService(IAuthDbContext context, ILogger<AuditLoginService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public Task AddAuditLogin(AuditLogin entity)
        {
            return this._context.AddAuditLogin(entity);
        }
    }
}
