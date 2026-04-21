namespace EIMSNext.Async.Abstractions.Messaging
{
    [Queue("export-log")]
    public class ExportLogTaskArgs
    {
        public string ExportLogId { get; set; } = string.Empty;

        public string CorpId { get; set; } = string.Empty;
    }
}
