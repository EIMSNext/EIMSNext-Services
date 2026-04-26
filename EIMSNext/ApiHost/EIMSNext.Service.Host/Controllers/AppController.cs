using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ApiService;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core;
using EIMSNext.Service;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.Models;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
	[ApiVersion(1.0)]
	public class AppController(IResolver resolver) : ApiControllerBase<AppApiService, App, AppViewModel>(resolver)
	{
	    [HttpPost("CreateGroup")]
	    public ActionResult<App> CreateGroup([FromBody] CreateAppGroupRequest request)
	    {
	        if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.Name))
	        {
	            return BadRequest("分组名称不能为空");
	        }

	        var appRepo = Resolver.GetRepository<App>();
	        var app = appRepo.Get(request.AppId);
	        if (app == null)
	        {
	            return NotFound();
	        }

	        var menu = new AppMenu
	        {
	            MenuId = Guid.NewGuid().ToString("N"),
	            Title = request.Name.Trim(),
	            MenuType = FormType.Group,
	            SortIndex = (app.AppMenus.Count + 1) * 100,
	            SubMenus = []
	        };

	        app.AppMenus.Add(menu);
	        AppMenuHelper.Normalize(app.AppMenus);
	        appRepo.Replace(app);
	        return Ok(app);
	    }

	    [HttpPost("EditGroup")]
	    public ActionResult<App> EditGroup([FromBody] EditAppGroupRequest request)
	    {
	        if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.MenuId) || string.IsNullOrWhiteSpace(request.Name))
	        {
	            return BadRequest("分组名称不能为空");
	        }

	        var appRepo = Resolver.GetRepository<App>();
	        var app = appRepo.Get(request.AppId);
	        if (app == null)
	        {
	            return NotFound();
	        }

	        var menu = AppMenuHelper.FindMenu(app.AppMenus, request.MenuId);
	        if (menu == null || menu.MenuType != FormType.Group)
	        {
	            return BadRequest("分组不存在");
	        }

	        menu.Title = request.Name.Trim();
	        appRepo.Replace(app);
	        return Ok(app);
	    }

	    [HttpPost("EditMenu")]
	    public ActionResult<App> EditMenu([FromBody] EditAppMenuRequest request)
	    {
	        if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.MenuId) || string.IsNullOrWhiteSpace(request.Name))
	        {
	            return BadRequest("菜单名称不能为空");
	        }

	        var appRepo = Resolver.GetRepository<App>();
	        var app = appRepo.Get(request.AppId);
	        if (app == null)
	        {
	            return NotFound();
	        }

	        var menu = AppMenuHelper.FindMenu(app.AppMenus, request.MenuId);
	        if (menu == null)
	        {
	            return BadRequest("菜单不存在");
	        }

	        if (menu.MenuType == FormType.Group)
	        {
	            return BadRequest("分组请使用专用接口修改");
	        }

	        var name = request.Name.Trim();
	        menu.Title = name;
	        menu.Icon = request.Icon ?? string.Empty;
	        menu.IconColor = request.IconColor ?? string.Empty;

	        if (menu.MenuType == FormType.Form)
	        {
	            var formRepo = Resolver.GetRepository<FormDef>();
	            var form = formRepo.Get(request.MenuId);
	            if (form == null)
	            {
	                return BadRequest("表单不存在");
	            }

	            form.Name = name;
	            formRepo.Replace(form);
	        }
	        else if (menu.MenuType == FormType.Dashboard)
	        {
	            var dashRepo = Resolver.GetRepository<DashboardDef>();
	            var dash = dashRepo.Get(request.MenuId);
	            if (dash == null)
	            {
	                return BadRequest("仪表盘不存在");
	            }

	            dash.Name = name;
	            dashRepo.Replace(dash);
	        }

	        appRepo.Replace(app);
	        return Ok(app);
	    }

	    [HttpPost("DeleteGroup")]
	    public ActionResult<App> DeleteGroup([FromBody] DeleteAppGroupRequest request)
	    {
	        if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.MenuId))
	        {
	            return BadRequest();
	        }

	        var appRepo = Resolver.GetRepository<App>();
	        var app = appRepo.Get(request.AppId);
	        if (app == null)
	        {
	            return NotFound();
	        }

	        var menu = AppMenuHelper.FindMenu(app.AppMenus, request.MenuId);
	        if (menu == null)
	        {
	            return BadRequest("分组不存在");
	        }

	        if (menu.MenuType != FormType.Group)
	        {
	            return BadRequest("只能删除分组");
	        }

	        if (menu.SubMenus?.Count > 0)
	        {
	            return BadRequest("当前分组下存在子菜单，不能删除");
	        }

	        if (!AppMenuHelper.RemoveMenu(app.AppMenus, request.MenuId))
	        {
	            return BadRequest("删除失败");
	        }

	        AppMenuHelper.Normalize(app.AppMenus);
	        appRepo.Replace(app);
	        return Ok(app);
	    }

	    [HttpPost("SaveMenus")]
	    public ActionResult<App> SaveMenus([FromBody] SaveAppMenusRequest request)
	    {
	        if (string.IsNullOrWhiteSpace(request.AppId))
	        {
	            return BadRequest();
	        }

	        if (!AppMenuHelper.ValidateTree(request.AppMenus))
	        {
	            return BadRequest("分组内不能再包含分组");
	        }

	        var appRepo = Resolver.GetRepository<App>();
	        var app = appRepo.Get(request.AppId);
	        if (app == null)
	        {
	            return NotFound();
	        }

	        app.AppMenus = AppMenuHelper.Normalize(request.AppMenus ?? []);
	        appRepo.Replace(app);
	        return Ok(app);
	    }
	}
}
