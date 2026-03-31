using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class PrintTemplateApiService(IResolver resolver) : ApiServiceBase<PrintTemplate, PrintTemplateViewModel, IPrintTemplateService>(resolver)
	{
	}
}
