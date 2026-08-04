namespace EIMSNext.Service.Host.Requests
{
    public class FieldChangeLogDeleteRequest
    {
        public List<string>? FieldIds { get; set; }

        public bool ClearAll { get; set; }
    }
}
