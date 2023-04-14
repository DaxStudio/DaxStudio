using DaxStudio.UI.Converters;
using System.ComponentModel;

namespace DaxStudio.UI.Enums
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum CustomTraceOutput
    {
        [Description("Grid")]
        Grid,
        [Description("File")]
        File,
        [Description("File + Grid preview")]
        FileAndGrid
    }
}
