using DaxStudio.Controls;
using DaxStudio.UI.ViewModels;
using System.Windows;

namespace DaxStudio.UI.Controls
{
    internal class PhysicalQueryPlanTreeGrid : GenericTreeGrid<PhysicalQueryPlanRow>
    {
        static PhysicalQueryPlanTreeGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PhysicalQueryPlanTreeGrid), new FrameworkPropertyMetadata(typeof(PhysicalQueryPlanTreeGrid)));
        }
    }
}
