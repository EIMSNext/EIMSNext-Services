namespace EIMSNext.Service.Contracts
{
    public interface IAppInstallService
    {
        Task<string> InstallAsync(string appProfileId);
    }
}
