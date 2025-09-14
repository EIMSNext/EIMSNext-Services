using IdentityServer4.Models;
using IdentityServer4.Stores;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Mappers;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Auth.Stores
{
    public class PersistedGrantStore : IPersistedGrantStore
    {
        private readonly IAuthDbContext _context;
        private readonly ILogger<PersistedGrantStore> _logger;

        public PersistedGrantStore(IAuthDbContext context, ILogger<PersistedGrantStore> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task StoreAsync(PersistedGrant token)
        {
            try
            {
                _logger.LogDebug("Try to save or update {persistedGrantKey} in database", token.Key);
                await _context.InsertOrUpdatePersistedGrant(t => t.Id == token.Key, token.ToEntity());
                _logger.LogDebug("{persistedGrantKey} stored in database", token.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Exception storing persisted grant");
                throw;
            }
        }

        public Task<PersistedGrant?> GetAsync(string key)
        {
            var persistedGrant = _context.PersistedGrants.FirstOrDefault(x => x.Id == key);
            var model = persistedGrant?.ToModel();

            _logger.LogDebug("{persistedGrantKey} found in database: {persistedGrantKeyFound}", key, model != null);

            return Task.FromResult(model);
        }

        public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
        {
            Validate(filter);

            var persistedGrants = _context.PersistedGrants.Where(
                x => (string.IsNullOrWhiteSpace(filter.SubjectId) || x.UserId == filter.SubjectId) &&
                                  (string.IsNullOrWhiteSpace(filter.ClientId) || x.ClientId == filter.ClientId) &&
                                  (string.IsNullOrWhiteSpace(filter.Type) || x.Type == filter.Type)).ToList();

            var model = persistedGrants.Select(x => x.ToModel());

            _logger.LogDebug($"{persistedGrants.Count} persisted grants found for filter: {filter}");

            return Task.FromResult(model);
        }

        public Task RemoveAsync(string key)
        {
            _logger.LogDebug("removing {persistedGrantKey} persisted grant from database", key);

            _context.RemovePersistedGrant(x => x.Id == key);

            return Task.FromResult(0);
        }

        public Task RemoveAllAsync(PersistedGrantFilter filter)
        {
            Validate(filter);

            _logger.LogDebug($"removing persisted grants from database for filter: {filter}");

            _context.RemovePersistedGrant(
                x => (string.IsNullOrWhiteSpace(filter.SubjectId) || x.UserId == filter.SubjectId) &&
                     (string.IsNullOrWhiteSpace(filter.ClientId) || x.ClientId == filter.ClientId) &&
                     (string.IsNullOrWhiteSpace(filter.Type) || x.Type == filter.Type));

            return Task.FromResult(0);
        }

        private void Validate(PersistedGrantFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            if (string.IsNullOrWhiteSpace(filter.ClientId) &&
                string.IsNullOrWhiteSpace(filter.SubjectId) &&
                string.IsNullOrWhiteSpace(filter.Type))
            {
                throw new ArgumentException("No filter values set.", nameof(filter));
            }
        }
    }
}