using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface ISerialNoSequenceService : IService<SerialNoSequence>
    {
        string NextCorpCode(PlatformType platform);

        /// <summary>
        /// 获取下一个表单级流水号计数(纯计数号,由调用方按需拼接)
        /// </summary>
        /// <param name="corpId">企业Id</param>
        /// <param name="appId">应用Id</param>
        /// <param name="formId">表单Id</param>
        /// <param name="key">同表单内流水号字段的标识(默认采用 fieldId)</param>
        /// <param name="cycle">重置周期</param>
        /// <returns>下一个计数号(从 1 开始)</returns>
        int NextFormSerialNo(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle);
    }
}
