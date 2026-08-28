using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class PaymentApiService(IResolver resolver) : ApiServiceBase<Payment, PaymentViewModel, IPaymentService>(resolver)
	{
	}
}
