using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Utils
{
    class PanesStyleSelector : StyleSelector
    {
        public Style ToolStyle
        {
            get;
            set;
        }

        public Style DocumentStyle { get; set; }

        public override System.Windows.Style SelectStyle(object item, System.Windows.DependencyObject container)
        {
            if (item is IToolWindow)
                return ToolStyle;

            if (item is IDaxDocument)
                return DocumentStyle;

            return base.SelectStyle(item, container);
        }
    }
}
