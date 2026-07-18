using Asp.Versioning;
using EIMSNext.ApiHost.Controllers;
using EIMSNext.Core;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EIMSNext.File.Host.Controllers
{
    [ApiVersion(1.0)]
    public class UploadController : MefControllerBase
    {
        private static readonly HashSet<string> BlockedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".ashx", ".asmx", ".aspx", ".bat", ".cgi", ".cmd", ".com", ".cpl",
            ".dll", ".exe", ".htm", ".html", ".hta", ".jar", ".js", ".jse",
            ".jsp", ".jspx", ".msi", ".msp", ".php", ".pl", ".ps1", ".psm1",
            ".py", ".scr", ".sh", ".svg", ".vbe", ".vbs", ".wsf", ".wsh", ".xhtml"
        };

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
        [RequestFormLimits(MultipartBodyLengthLimit = 1024 * 1024 * 1024)]
        [RequestSizeLimit(1024 * 1024 * 1024)]
        public async Task<IActionResult> Upload()
        {
            var files = Request.Form.Files;
            _logger.LogDebug("收到上传文件 {FileCount} 个", files.Count);

            if (files.Count == 0)
            {
                return BadRequest("请至少选择一个文件");
            }

            var blockedFile = files.FirstOrDefault(file => BlockedFileExtensions.Contains(Path.GetExtension(file.FileName)));
            if (blockedFile != null)
            {
                return BadRequest($"不允许上传文件类型 {Path.GetExtension(blockedFile.FileName)}");
            }

            foreach (var file in files)
            {
                var validationError = await ValidateFileContent(file);
                if (validationError != null)
                {
                    return BadRequest(validationError);
                }
            }

            var streams = new List<Stream>(files.Count);
            IReadOnlyList<UploadedFile> attachments;
            try
            {
                streams.AddRange(files.Select(file => file.OpenReadStream()));
                attachments = _uploadService.Upload(
                    files.Select((file, index) => new UploadedFileUpload(
                        streams[index],
                        file.FileName,
                        Convert.ToInt64(Math.Floor(file.Length / 1000.0)))),
                    IdentityContext.CurrentCorpId);
            }
            finally
            {
                streams.ForEach(stream => stream.Dispose());
            }

            return Ok(new
            {
                value = attachments.Select(x =>
                {
                    return new
                    {
                        x.Id,
                        x.FileName,
                        x.SavePath,
                        x.ThumbPath,
                        x.FileExt,
                        x.FileSize,
                        url = x.SavePath,
                        thumbUrl = x.ThumbPath,
                    };
                })
            });
        }

        private static async Task<string?> ValidateFileContent(IFormFile file)
        {
            const int sniffLength = 64 * 1024;
            await using var stream = file.OpenReadStream();
            var buffer = new byte[(int)Math.Min(sniffLength, Math.Max(1L, file.Length))];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
                if (count == 0) break;
                read += count;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var text = Encoding.UTF8.GetString(buffer, 0, read);
            var activeMarker = new[] { "<script", "<html", "<!doctype html", "<svg", "<?php", "javascript:", "vbscript:" }
                .FirstOrDefault(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
            if (activeMarker != null)
            {
                return $"文件内容包含主动内容标记 {activeMarker}";
            }

            if (!HasExpectedSignature(extension, buffer.AsSpan(0, read)))
            {
                return $"文件内容与扩展名 {extension} 不匹配";
            }

            return null;
        }

        private static bool HasExpectedSignature(string extension, ReadOnlySpan<byte> content)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => StartsWith(content, [0xff, 0xd8, 0xff]),
                ".png" => StartsWith(content, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
                ".gif" => StartsWith(content, "GIF8"u8),
                ".webp" => content.Length >= 12 && StartsWith(content, "RIFF"u8) && content[8..].StartsWith("WEBP"u8),
                ".pdf" => StartsWith(content, "%PDF-"u8),
                ".zip" or ".docx" or ".xlsx" or ".pptx" => StartsWith(content, [0x50, 0x4b, 0x03, 0x04]),
                _ => true,
            };
        }

        private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) => content.StartsWith(signature);
    }
}
