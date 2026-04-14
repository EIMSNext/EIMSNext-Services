using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class ExportLogApiService(IResolver resolver)
        : ApiServiceBase<ExportLog, ExportLogViewModel, IExportLogService>(resolver)
    {
        protected override IQueryable<ExportLogViewModel> FilterByPermission()
        {
            var empId = IdentityContext.CurrentEmployee?.Id ?? string.Empty;
            return base.FilterByPermission().Where(x => x.CreateBy != null && x.CreateBy.Value == empId);
        }
    }
}
