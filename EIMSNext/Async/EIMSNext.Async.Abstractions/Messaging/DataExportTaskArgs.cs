namespace EIMSNext.Async.Abstractions.Messaging
{
    [Queue("data-export")]
    public class DataExportTaskArgs
    {
        public string ExportLogId { get; set; } = string.Empty;

        public string CorpId { get; set; } = string.Empty;
    }
}
