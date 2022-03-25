using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Standalone
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal class BuildDateAttribute: Attribute
    {
        public BuildDateAttribute(string value)
        {
            DateTime = DateTime.ParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }
        public DateTime DateTime { get; }
    }
}
