using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class VersionExtensions
    {
        private static Version zeroVersion = new Version(0, 0, 0, 0);

        public static bool IsNotSet(this Version version)
        {
            if (version == null) return true;
            return version == zeroVersion;
        }
    }
}
