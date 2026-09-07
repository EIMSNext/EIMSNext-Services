using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 流水号序列的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class SerialNoSequenceApiService(IResolver resolver) : ApiServiceBase<SerialNoSequence, SerialNoSequenceViewModel, ISerialNoSequenceService>(resolver)
	{
	}
}
