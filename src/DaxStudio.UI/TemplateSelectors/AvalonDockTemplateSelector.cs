using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.TemplateSelectors
{
    public class AvalonDockTemplateSelector : DataTemplateSelector
    {
        public AvalonDockTemplateSelector() { }

        public DataTemplate DocumentViewTemplate { get; set; }

        //When this method is called, item is always a ContentPresenter
        //ContentPresenter.Content will contain the ViewModel I add
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DocumentViewModel)
            {

                return DocumentViewTemplate;
            }

            //delegate the call to base class
            return base.SelectTemplate(item, container);
        }
    }
}
