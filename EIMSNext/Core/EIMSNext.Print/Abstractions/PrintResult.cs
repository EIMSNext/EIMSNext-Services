using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Utilities;

namespace EIMSNext.Print.Abstractions
{
    public class PrintResult
    {
        public byte[] Content { get; internal set; } = Array.Empty<byte>();
        public string FileName { get; internal set; } = string.Empty;
    }
}
