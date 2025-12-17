using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EIMSNext.Plugin.Interface
{
    public class PluginMetadata
    {
        public required string Id { get; set; }
        public required string Version { get; set; }
    }
}
