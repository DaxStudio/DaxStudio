using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class IntegerExtensions
    {
        private const int MILLISECONDS_PER_SECOND = 1000;
        public static int SecondsToMilliseconds(this int seconds)
        {
        return seconds * MILLISECONDS_PER_SECOND;
        }
    }
}
