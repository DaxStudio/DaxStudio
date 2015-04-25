using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Utils
{

    public class AutobinderTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Template { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var win = item as IToolWindow;
            if (win != null)
                return Template;
            return null;
        }
    }

    public class AutobinderTemplate : DataTemplate
    {        
   
    }
}

