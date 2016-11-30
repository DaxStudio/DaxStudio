using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.TemplateSelectors
{

    public class ToolWindowTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Template { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var win = item as IToolWindow;
            if (win != null)
                return Template;

            //delegate the call to base class
            return base.SelectTemplate(item, container);
        }
    }

    public class AutobinderTemplate : DataTemplate
    {        
   
    }
}

