using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Api.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Api.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class FormNotifyController(IResolver resolver) : ODataController<FormNotifyApiService, FormNotify, FormNotifyViewModel, FormNotifyRequest>(resolver)
	{
		
	}
}
