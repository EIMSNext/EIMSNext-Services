using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using JianJieYun.Print.Common.Extension;
using NLog;

namespace EIMSNext.Print.Common
{
    public abstract class BasePrintGenerator<T> : IPrintGenerator where T : IPrintGenerator
    {
        protected ILogger Loggger = LogManager.GetCurrentClassLogger();
        protected bool IsPreview { get; set; } = false;

        public byte[] Preview(PrintTemplate template, PrintOption option)
        {
            IsPreview = true;
            return Generate(template, option, new List<JsonObject> { });
        }

        public byte[] Print(PrintTemplate template, PrintOption option, List<object> datas)
        {
            return Generate(template, option, datas.ConvertToJsonObject());
        }

        protected abstract byte[] Generate(PrintTemplate template, PrintOption option, List<JsonObject> datas);

    }
}
