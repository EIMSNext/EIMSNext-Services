using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 
    /// </summary>
    public class FormDataUpdateLog : CorpEntityBase
    {
        /// <summary>
        /// 
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public List<UpdateLogItem> Items { get; set; } = new List<UpdateLogItem>();
    }
    /// <summary>
    /// 
    /// </summary>
    public class UpdateLogItem
    {
        /// <summary>
        /// 
        /// </summary>
        public long UpdateTime { get; set; } = 0;
        /// <summary>
        /// 
        /// </summary>
        public Operator? UpdateBy { get; set; }
    }
}
