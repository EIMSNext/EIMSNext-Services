using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EIMSNext.ApiService.RequestModel;

namespace EIMSNext.ApiService.Interface
{
    public interface IAggregateApiService: IApiService
    {
        dynamic Calucate(AggCalcRequest request);
    }
}
