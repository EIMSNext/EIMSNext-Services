namespace EIMSNext.Service.Contracts
{
    public interface IAppPublishService
    {
        Task<AppPublishResult> PublishAsync(string appDefId);
    }
}
