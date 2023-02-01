using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    public enum VpaTableColumnDisplay
    {
        [Description("combined as Table-Column")]
        TableDashColumn,
        [Description("combined as table[column]")]
        DaxNameFormat,
        [Description("separate columns")]
        TwoColumns
    }
}
