using System.Text.Json.Nodes;
using EIMSNext.Print.Extensions;
using NLog;

namespace EIMSNext.Print.Abstractions
{
    public abstract class BasePrintGenerator<T> : IPrintGenerator where T : IPrintGenerator
    {
        protected ILogger Loggger = LogManager.GetCurrentClassLogger();
        protected bool IsPreview { get; set; } = false;

        public PrintResult Preview(PrintTemplate template, PrintOption option)
        {
            IsPreview = true;
            var content = Generate(template, option, new List<JsonObject> { });
            return new PrintResult { Content = content, FileName = GetFileName(null, option) };
        }

        public PrintResult Print(PrintTemplate template, PrintOption option, IEnumerable<object> datas)
        {
            var content = Generate(template, option, datas.ConvertToJsonObject());
            return new PrintResult { Content = content, FileName = GetFileName(datas.FirstOrDefault(), option) };
        }

        protected abstract byte[] Generate(PrintTemplate template, PrintOption option, List<JsonObject> datas);

        protected virtual string GetFileName(object? data, PrintOption option)
        {
            var fileName = IsPreview ? "preview" : "print";

            var time = DateTime.Now.ToString("yyyyMMddhhmmssfff");
            return $"{fileName}_{time}.pdf";
        }

    }
}
