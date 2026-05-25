using System.ComponentModel;
using DaxStudio.Core.Converters;

namespace DaxStudio.Core.Exports
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum CsvEncoding
    {
        [Description("UTF-8")]
        UTF8,
        [Description("Unicode (UTF-16)")]
        Unicode
    }
}
