using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
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
