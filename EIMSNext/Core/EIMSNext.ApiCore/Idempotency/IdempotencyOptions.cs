namespace EIMSNext.ApiCore.Idempotency;

public sealed class IdempotencyOptions
{
    public bool Enabled { get; set; } = true;
    public bool RequireKey { get; set; }
    /// <summary>How long completed idempotency responses remain replayable.</summary>
    public int TtlMinutes { get; set; } = 10;
    public int ProcessingTimeoutSeconds { get; set; } = 120;
    public long MaxResponseBodyBytes { get; set; } = 1024 * 1024;
    public bool FailOpenWhenStoreUnavailable { get; set; } = true;
    public string[] ExcludedPathPrefixes { get; set; } = ["/connect", "/identity/logout", "/identity/send", "/upload"];
}
