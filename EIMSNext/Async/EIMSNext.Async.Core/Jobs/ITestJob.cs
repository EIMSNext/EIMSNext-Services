using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Async.Core.Jobs
{
    /// <summary>
    /// 测试作业业务接口（便于单元测试）
    /// </summary>
    public interface ITestJob
    {
        Task ExecuteAsync();
    }
}
