using EIMSNext.Auth.Entities;

namespace EIMSNext.Auth.Integrations.Abstractions
{
    public abstract class IntegrationProviderBase
    {
        protected static string Encode(string value)
        {
            return Uri.EscapeDataString(value);
        }

        protected static string GetRequiredCode(string? code, string type)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException($"{type}未返回有效授权码");
            }

            return code;
        }

        protected static string GetExtraParameter(IntegrationLoginSetting setting, string key)
        {
            return setting.ExtraParameters.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        }

        protected static string GetClientId(IntegrationLoginSetting setting)
        {
            return !string.IsNullOrWhiteSpace(setting.ClientId) ? setting.ClientId : setting.AppId;
        }

        protected static string GetClientSecret(IntegrationLoginSetting setting)
        {
            return !string.IsNullOrWhiteSpace(setting.ClientSecret) ? setting.ClientSecret : setting.AppSecret;
        }
    }
}
