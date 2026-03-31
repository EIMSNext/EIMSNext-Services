using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Print.Abstractions
{
    public interface IPrintGenerator
    {
        byte[] Preview(PrintTemplate template, PrintOption option);
        byte[] Print(PrintTemplate template, PrintOption option, List<object> datas);
    }
}
