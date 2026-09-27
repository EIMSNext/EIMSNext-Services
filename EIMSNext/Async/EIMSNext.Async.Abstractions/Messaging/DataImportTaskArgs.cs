namespace EIMSNext.Async.Abstractions.Messaging
{
    [Queue("data-import")]
    public class DataImportTaskArgs
    {
        public string ImportLogId { get; set; } = string.Empty;

        public string CorpId { get; set; } = string.Empty;

        public int RetryCount { get; set; }
    }
}
