using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 导出日志的 API 服务。
    /// </summary>
    /// <param name="resolver">服务解析器。</param>
    public class ExportLogApiService(IResolver resolver)
        : ApiServiceBase<ExportLog, ExportLogViewModel, IExportLogService>(resolver)
    {
        /// <summary>
        /// 按当前身份权限过滤查询。
        /// </summary>
        protected override IQueryable<ExportLogViewModel> FilterByPermission()
        {
            var empId = IdentityContext.CurrentEmployee?.Id ?? string.Empty;
            return base.FilterByPermission().Where(x => x.CreateBy != null && x.CreateBy.Id == empId);
        }
    }
}
