using Asp.Versioning;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using HKH.Mef2.Integration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NanoidDotNet;

namespace EIMSNext.FileUploadApi.Controllers
{
    [Authorize]
    [ApiController, ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UploadController : MefControllerBase
    {
        private readonly ILogger<UploadController> _logger;
        private readonly IUploadedFileService _uploadService;

        public UploadController(IResolver resolver) : base(resolver)
        {
            _logger = resolver.GetLogger<UploadController>();
            _uploadService = resolver.Resolve<IUploadedFileService>();
        }

        /// <summary>
        /// 上传附件
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Upload()
        {
            var files = Request.Form.Files;
            _logger.LogDebug($"收到{files.Count}个上传的文件");

            var attachments = new List<UploadedFile>();
            foreach (var file in files)
            {
                var fileExt = new FileInfo(file.FileName).Extension;
                var saveName = GeneratFileName() + fileExt;
                var savePath = $"{AppSetting.FileBasePath}\\{IdentityContext.CurrentCorpId}\\{saveName}";
                var thumbPath = $"{AppSetting.FileBasePath}\\{IdentityContext.CurrentCorpId}\\thumb\\{saveName}";

                var attachment = new UploadedFile() { FileName = file.FileName, SavePath = savePath, ThumbPath = thumbPath, FileExt = fileExt, FileSize = Convert.ToInt64(Math.Floor(file.Length / 1000.0)) };

                var saveFolder = Path.Combine(Common.Constants.WebRootPath, AppSetting.FileBasePath, IdentityContext.CurrentCorpId);
                if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                var thumbFolder = Path.Combine(saveFolder, "thumb");
                if (!Directory.Exists(thumbFolder)) Directory.CreateDirectory(thumbFolder);

                var saveToPath = Path.Combine(Common.Constants.WebRootPath, savePath);
                _logger.LogDebug($"{saveToPath}");

                using (var targetStream = System.IO.File.Create(saveToPath))
                {
                    await file.CopyToAsync(targetStream);
                }

                //TODO:生成缩略图

                attachments.Add(attachment);
            }

            _uploadService.Add(attachments);

            return Ok(new
            {
                value = attachments.Select(x => new { x.Id, x.FileName, x.SavePath, x.ThumbPath, x.FileExt, x.FileSize })
            });
        }

        private string GeneratFileName()
        {
            var alphabet = "_0123456789abcdefghijklmnopqrstuvwxyz";
            return Nanoid.Generate(alphabet, 24);
        }
    }
}
