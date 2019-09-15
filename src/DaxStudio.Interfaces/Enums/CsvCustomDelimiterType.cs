using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    public enum CustomCsvDelimiterType
    {
        [Description("Uses the default list separator character for the current culture")]
        CultureDefault,
        [Description("Uses comma as a delimiter")]
        Comma,
        [Description("Uses tab as a delimiter ")]
        Tab,
        [Description("Uses the specified other delimiter")]
        Other
    }
}
