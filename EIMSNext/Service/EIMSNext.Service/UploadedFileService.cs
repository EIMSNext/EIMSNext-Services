using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class UploadedFileService : EntityServiceBase<UploadedFile>, IUploadedFileService
    {
        public UploadedFileService(IResolver resolver) : base(resolver)
        {
        }
    }
}
