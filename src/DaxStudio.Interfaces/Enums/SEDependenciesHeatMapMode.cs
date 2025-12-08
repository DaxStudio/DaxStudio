using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    /// <summary>
    /// Defines what metric is used for the table header heat map coloring
    /// in the Storage Engine Dependencies view.
    /// </summary>
    public enum SEDependenciesHeatMapMode
    {
        /// <summary>
        /// Color based on CPU time consumed by queries on this table.
        /// </summary>
        [Description("CPU Time")]
        CpuTime,

        /// <summary>
        /// Color based on how many times the table appears in SE queries (Hit Count).
        /// </summary>
        [Description("Hit Count")]
        HitCount,

        /// <summary>
        /// Color based on total rows scanned by queries on this table.
        /// </summary>
        [Description("Row Count")]
        RowCount
    }
}
