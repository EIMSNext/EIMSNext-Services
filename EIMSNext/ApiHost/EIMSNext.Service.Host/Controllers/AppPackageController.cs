using Asp.Versioning;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.Requests;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;

namespace EIMSNext.Service.Host.Controllers
{
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.PlatAdmin)]
    public class AppPackageController(IResolver resolver) : EIMSNext.ApiHost.Controllers.MefControllerBase(resolver)
    {
        private const int MaxPackageBytes = 10 * 1024 * 1024;
        private IAppPackageService PackageService => Resolver.Resolve<IAppPackageService>();

        [HttpGet("{appProfileId}/export")]
        public async Task<IActionResult> Export([FromRoute] string appProfileId)
        {
            var package = await PackageService.ExportAsync(appProfileId);
            return File(package.Content, "application/vnd.eimsnext.app-package", package.FileName);
        }

        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxPackageBytes)]
        [RequestSizeLimit(MaxPackageBytes)]
        public async Task<ActionResult> Preview([FromForm] AppPackageImportRequest request)
        {
            var file = RequirePackageFile(request.File);
            await using var stream = file.OpenReadStream();
            return Ok(await PackageService.PreviewAsync(stream, file.Length));
        }

        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxPackageBytes)]
        [RequestSizeLimit(MaxPackageBytes)]
        public async Task<ActionResult> Import([FromForm] AppPackageImportRequest request)
        {
            var file = RequirePackageFile(request.File);
            await using var stream = file.OpenReadStream();
            return Ok(await PackageService.ImportAsync(stream, file.Length));
        }

        private static IFormFile RequirePackageFile(IFormFile? file)
        {
            if (file == null || file.Length <= 0 || file.Length > MaxPackageBytes || !file.FileName.EndsWith(".eimsapp", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("请选择不超过 10 MB 的 .eimsapp 应用模板包");
            }
            return file;
        }
    }
}
