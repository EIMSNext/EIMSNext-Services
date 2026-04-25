using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Host.Models
{
    public class EditAppMenuRequest
    {
        public string AppId { get; set; } = string.Empty;
        public string MenuId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? IconColor { get; set; }
    }

    public class EditAppGroupRequest
    {
        public string AppId { get; set; } = string.Empty;
        public string MenuId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CreateAppGroupRequest
    {
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class DeleteAppGroupRequest
    {
        public string AppId { get; set; } = string.Empty;
        public string MenuId { get; set; } = string.Empty;
    }

    public class SaveAppMenusRequest
    {
        public string AppId { get; set; } = string.Empty;
        public List<AppMenu> AppMenus { get; set; } = [];
    }
}
