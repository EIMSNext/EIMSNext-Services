using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单数据更新日志
    /// </summary>
    public class FormDataUpdateLog : CorpEntityBase
    {
        /// <summary>
        /// 表单数据ID
        /// </summary>
        public string DataId { get; set; } = string.Empty;
        /// <summary>
        /// 更新日志明细列表
        /// </summary>
        public List<UpdateLogItem> Items { get; set; } = new List<UpdateLogItem>();
    }
    /// <summary>
    /// 更新日志明细
    /// </summary>
    public class UpdateLogItem
    {
        /// <summary>
        /// 更新时间戳
        /// </summary>
        public long UpdateTime { get; set; } = 0;
        /// <summary>
        /// 操作人信息
        /// </summary>
        public Operator? UpdateBy { get; set; }
    }
}
