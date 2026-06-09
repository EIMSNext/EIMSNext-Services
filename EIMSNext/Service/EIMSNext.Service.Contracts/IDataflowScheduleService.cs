using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    /// <summary>
    /// 数据流定时调度服务。
    /// </summary>
    public interface IDataflowScheduleService
    {
        /// <summary>
        /// 重建数据流调度。
        /// </summary>
        Task RebuildScheduleAsync(Wf_Definition definition, MongoDB.Driver.IClientSessionHandle? session = null);
    }
}
