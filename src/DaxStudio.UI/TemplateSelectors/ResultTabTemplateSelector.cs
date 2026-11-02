using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.TemplateSelectors
{
    /// <summary>
    /// Selects the content template for a tab in the query Results <c>TabControl</c>: a query-result
    /// data grid for a <see cref="DataTableResultTab"/>, or the SHOW tree-grid for a
    /// <see cref="ShowTreeResultTab"/>.
    /// </summary>
    public class ResultTabTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DataTableTemplate { get; set; }
        public DataTemplate ShowTreeTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ShowTreeResultTab) return ShowTreeTemplate;
            return DataTableTemplate;
        }
    }
}
