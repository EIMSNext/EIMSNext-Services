using EIMSNext.Entity;

namespace EIMSNext.ApiService.ViewModel
{
    public class RoleViewModel : Role
    {
        public RoleGroup? RoleGroup { get; set; }
    }
}

