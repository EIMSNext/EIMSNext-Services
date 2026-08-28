using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	public class SerialNoSequenceApiService(IResolver resolver) : ApiServiceBase<SerialNoSequence, SerialNoSequenceViewModel, ISerialNoSequenceService>(resolver)
	{
	}
}
