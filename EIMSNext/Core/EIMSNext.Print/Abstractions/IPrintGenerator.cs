using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Print.Abstractions
{
    public interface IPrintGenerator
    {
        PrintResult Preview(PrintTemplate template, PrintOption option);
        PrintResult Print(PrintTemplate template, PrintOption option, IEnumerable<object> datas);
    }
}
