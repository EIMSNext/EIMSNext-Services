namespace EIMSNext.Async.Tasks.System;

public interface ISystemTokenProvider
{
    Task<string> GetAccessTokenAsync(string corpId, string objectType, string objectId, CancellationToken cancellationToken = default);
}
