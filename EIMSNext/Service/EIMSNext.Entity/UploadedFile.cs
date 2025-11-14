using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 
    /// </summary>
    public class UploadedFile : CorpEntityBase
    {
        #region Public Properties

        /// <summary>                                                                                             
        /// 原文件名                                                                                                  
        /// </summary> 		
        public string FileName { get; set; } = string.Empty;

        /// <summary>                                                                                             
        /// 上传后文件地址(相对路径)                                                                                                  
        /// </summary> 		
        public string SavePath { get; set; } = string.Empty;

        /// <summary>
        /// 缩略图文件地址(相对路径)
        /// </summary>
        public string? ThumbPath { get; set; }

        /// <summary>                                                                                             
        /// 文件扩展名                                                                                                  
        /// </summary> 		
        public string? FileExt { get; set; }

        /// <summary>
        /// 文件大小 (K)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 引用计数
        /// </summary>
        public int RefCount { get; set; } = 0;

        #endregion
    }
}
