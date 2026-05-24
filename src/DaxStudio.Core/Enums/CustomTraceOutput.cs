using DaxStudio.Core.Converters;
using System.ComponentModel;

namespace DaxStudio.Core.Enums
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum CustomTraceOutput
    {
        [Description("Grid")]
        Grid,
        [Description("File")]
        File,
        [Description("File + Grid")]
        FileAndGrid
    }
}