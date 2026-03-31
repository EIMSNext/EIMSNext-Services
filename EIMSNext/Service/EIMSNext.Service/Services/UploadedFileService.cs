using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
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
