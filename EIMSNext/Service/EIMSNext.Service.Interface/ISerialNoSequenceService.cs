using EIMSNext.Core.Entity;
using EIMSNext.Core.Service;
using EIMSNext.Entity;

namespace EIMSNext.Service.Interface
{
    public interface ISerialNoSequenceService : IService<SerialNoSequence>
    {
        string NextCorpCode(PlatformType platform);
    }
}
