using System.ComponentModel;

namespace DaxStudio.Common.Enums
{
    public enum CustomCsvDelimiterType
    {
        [Description("Default list separator character")]
        CultureDefault,
        [Description("Comma")]
        Comma,
        [Description("Tab")]
        Tab,
        [Description("Other")]
        Other
    }
}
