using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Utils
{
    class PanesStyleSelector : StyleSelector
    {
        public Style ToolStyle
        {
            get;
            set;
        }

        public override System.Windows.Style SelectStyle(object item, System.Windows.DependencyObject container)
        {
            if (item is IToolWindow)
                return ToolStyle;

            return base.SelectStyle(item, container);
        }
    }
}
