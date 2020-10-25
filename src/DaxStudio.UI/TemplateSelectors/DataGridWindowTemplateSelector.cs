using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using AvalonDock.Layout;

namespace DaxStudio.UI.TemplateSelectors
{

    public class DataGridWindowTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DefaultTemplate { get; set; }

        public DataTemplate TraceTemplate { get; set; }

        public DataTemplate DataGridTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var win = item as LayoutAnchorable;
            if (win == null)
                //delegate the call to base class
                return base.SelectTemplate(item, container);

            var content = win.Content as ITraceWatcher;
            if (content != null)
                return TraceTemplate;

            var dataGridWindow = win.Content as IDataGridWindow;
            if (dataGridWindow != null)
                return DataGridTemplate;

            // for other Anchoralbe windows return the default template
            return DefaultTemplate;
        }
    }


}

