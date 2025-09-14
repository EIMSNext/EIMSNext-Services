using HKH.Mef2.Integration;

using EIMSNext.Core.Entity;

namespace EIMSNext.Flow.Core
{
    public sealed class WfServiceContext : IServiceContext
    {
        //private static IServiceContext _nobody = new WfServiceContext { Operator = new Operator(string.Empty, string.Empty, string.Empty, "nobody") };

        //public static IServiceContext NoBody => _nobody;
        public WfServiceContext(IResolver resolver)
        {
            Resolver = resolver;
            Operator = Operator.Empty;
            AccessToken = "";
            CorpId = "";
        }
        //private WfServiceContext(IResolver resolver)
        //{
        //    Resolver = resolver;
        //}

        private IResolver Resolver { get; set; }
        public Operator? Operator { get; set; }
        public IUser? User { get; set; }
        public IEmployee? Employee { get; set; }
        public string AccessToken { get; set; }
        public DataAction Action { get; set; }
        public string CorpId { get; set; }
    }
}
