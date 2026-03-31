using EIMSNext.Core.Entities;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Contracts
{
    public interface ISerialNoSequenceService : IService<SerialNoSequence>
    {
        string NextCorpCode(PlatformType platform);
    }
}
