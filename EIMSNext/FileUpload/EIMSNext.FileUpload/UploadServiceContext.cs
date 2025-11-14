using EIMSNext.Core.Entity;

namespace EIMSNext.FileUpload
{
    public class UploadServiceContext : IServiceContext
    {
        public UploadServiceContext()
        {
            Operator = Operator.Empty;
        }

        public Operator? Operator { get; set; }
        public IUser? User { get; set; }
        public IEmployee? Employee { get; set; }

        //TODO
        public string AccessToken { get; set; } = "";
        public string CorpId { get; set; } = "";
        public DataAction Action { get; set; }
    }
}
