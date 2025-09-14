using EIMSNext.Auth.Interfaces;
using IdentityServer4.Services;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Services
{
    public class CorsPolicyService : ICorsPolicyService
    {
        private readonly IAuthDbContext _context;
        private readonly ILogger<CorsPolicyService> _logger;


        public CorsPolicyService(IAuthDbContext context, ILogger<CorsPolicyService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public Task<bool> IsOriginAllowedAsync(string origin)
        {
            // If we use SelectMany directly, we got a NotSupportedException inside MongoDb driver.
            // Details: 
            // System.NotSupportedException: Unable to determine the serialization information for the collection 
            // selector in the tree: aggregate([]).SelectMany(x => x.AllowedCorsOrigins.Select(y => y.Origin))
            var origins = _context.Clients.Select(x => x.AllowedCorsOrigins.Select(y => y.Origin)).ToList();

            // As a workaround, we use SelectMany in memory.
            var distinctOrigins = origins.SelectMany(o => o).Where(x => x != null).Distinct();

            var isAllowed = distinctOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

            _logger.LogDebug("Origin {origin} is allowed: {originAllowed}", origin, isAllowed);

            return Task.FromResult(isAllowed);
        }
    }
}