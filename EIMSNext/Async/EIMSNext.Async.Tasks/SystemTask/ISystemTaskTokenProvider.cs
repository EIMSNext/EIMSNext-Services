namespace EIMSNext.Async.Tasks.SystemTask
{
    public interface ISystemTaskTokenProvider
    {
        Task<string> GetAccessTokenAsync(string corpId, string objectType, string objectId, CancellationToken cancellationToken = default);
    }
}
