using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Api.OData;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService;

namespace EIMSNext.Service.Api.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class PaymentController(IResolver resolver) : ReadOnlyODataController<PaymentApiService, Payment, PaymentViewModel>(resolver)
	{
		
	}
}
