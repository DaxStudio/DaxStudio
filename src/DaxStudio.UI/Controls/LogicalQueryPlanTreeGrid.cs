using DaxStudio.Controls;
using DaxStudio.UI.ViewModels;
using System.Windows;

namespace DaxStudio.UI.Controls
{
    internal class LogicalQueryPlanTreeGrid : GenericTreeGrid<LogicalQueryPlanRow>
    {
        static LogicalQueryPlanTreeGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LogicalQueryPlanTreeGrid), new FrameworkPropertyMetadata(typeof(LogicalQueryPlanTreeGrid)));
        }
    }
}
