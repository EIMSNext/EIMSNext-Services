using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Core;
using HKH.Mef2.Integration;

namespace EIMSNext.Auth.Services
{
    public sealed class IntegrationProviderResolver(IResolver resolver) : IIntegrationProviderResolver
    {
        private readonly Lazy<IReadOnlyDictionary<string, IIntegrationProvider>> _providers = new(() =>
            resolver.GetExports<Lazy<IIntegrationProvider, Dictionary<string, object>>>()
                .Where(x => x.Metadata.TryGetValue(MefMetadata.Id, out var id) && !string.IsNullOrWhiteSpace(id?.ToString()))
                .GroupBy(x => x.Metadata[MefMetadata.Id].ToString()!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase));

        public bool TryGetById(string id, out IIntegrationProvider? provider)
        {
            provider = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return _providers.Value.TryGetValue(id, out provider);
        }
    }
}
