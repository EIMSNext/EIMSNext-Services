using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    public class RoleViewModel : Role
    {
        public RoleGroup? RoleGroup { get; set; }
    }
}

