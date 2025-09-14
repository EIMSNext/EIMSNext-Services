namespace EIMSNext.ApiService.RequestModel
{
    public class AppRequest : RequestBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string Icon { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public int SortIndex { get; set; }
    }
}

