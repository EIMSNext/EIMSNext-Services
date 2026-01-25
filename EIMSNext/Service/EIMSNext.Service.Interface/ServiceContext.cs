using EIMSNext.Auth.Entity;
using EIMSNext.Cache;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Entity;
using HKH.Mef2.Integration;

namespace EIMSNext.Service.Interface
{
    public sealed class ServiceContext : IServiceContext
    {
        private bool _userRetrieved = false;
        private User? _user;
        private bool _empRetrieved = false;
        private Employee? _employee;

        public ServiceContext(IResolver resolver)
        {
            Resolver = resolver;
            AccessToken = "";
            CorpId = "";
            SessionStore = resolver.Resolve<ISessionStore>();
        }

        private IResolver Resolver { get; set; }
        public ISessionStore SessionStore { get; private set; }
        public Operator? Operator { get; set; }

        public IUser? User
        {
            get
            {
                if (_user == null && !_userRetrieved) RetrieveUser();
                return _user;
            }
            set
            {
                _user = (User?)value;
                _userRetrieved = _user != null;
            }
        }
        public IEmployee? Employee
        {
            get
            {
                if (_employee == null && !_empRetrieved) RetrieveEmp();
                return _employee;
            }
            set
            {
                _employee = (Employee?)value;
                _empRetrieved = _employee != null;
            }
        }

        public string AccessToken { get; set; }
        public string? ClientIp {  get; set; }
        public DataAction Action { get; set; }
        public string CorpId { get; set; }

        private void RetrieveUser()
        {
            if (Resolver == null)
                throw new ArgumentNullException("Resolver");

            if (!_userRetrieved && !string.IsNullOrEmpty(Operator?.UserId))
            {
                _user = Resolver.GetService<User>().Get(Operator.UserId);
                _userRetrieved = true;
            }
        }
        private void RetrieveEmp()
        {
            if (Resolver == null)
                throw new ArgumentNullException("Resolver");

            if (!_empRetrieved && !string.IsNullOrEmpty(Operator?.EmpId))
            {
                _employee = Resolver.GetService<Employee>().Get(Operator.EmpId);
                _empRetrieved = true;
            }
        }
    }
}
