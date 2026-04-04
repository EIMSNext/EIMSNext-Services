using EIMSNext.Print.Abstractions;

namespace EIMSNext.Print
{
    public class CustomPrintService
    {
        public PrintResult Preview(PrintTemplate template, PrintOption option)
        {
           return new Pdf.PdfGenerator().Preview(template, option);
        }

        public PrintResult Print(PrintTemplate template, PrintOption option, IEnumerable<object> datas)
        {
            return new Pdf.PdfGenerator().Print(template, option, datas);
        }
    }
}
