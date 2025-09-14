using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Mappers;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Stores
{
    public class ClientStore : IClientStore
    {
        private readonly IAuthDbContext _context;
        private readonly ILogger<ClientStore> _logger;

        public ClientStore(IAuthDbContext context, ILogger<ClientStore> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        public Task<Client?> FindClientByIdAsync(string clientId)
        {
            var client = _context.Clients.FirstOrDefault(x => x.Id == clientId);

            var model = client?.ToModel();

            _logger.LogDebug("{clientId} found in database: {clientIdFound}", clientId, model != null);

            return Task.FromResult(model);
        }
    }
}