namespace EIMSNext.Service.Contracts
{
    public interface IAppPublishService
    {
        Task<string> PublishAsync(string appDefId);
    }
}
