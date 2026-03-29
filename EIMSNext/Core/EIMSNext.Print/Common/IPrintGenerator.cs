using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Print.Common
{
    public interface IPrintGenerator
    {
        byte[] Preview(PrintTemplate template, PrintOption option);
        byte[] Print(PrintTemplate template, PrintOption option, List<object> datas);
    }
}
