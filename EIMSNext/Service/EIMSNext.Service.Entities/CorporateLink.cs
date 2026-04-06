using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 企业关联实体，用于表示企业之间的关联关系
    /// </summary>
    public class CorporateLink : CorpEntityBase
    {
        /// <summary>
        /// 关联企业ID
        /// </summary>
        public string LinkCorpId { get; set; } = string.Empty;
    }
}
