using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DaxStudio.UI.AttachedProperties
{
    public class BrushBindingHelper
    {

        private static void ForegroundResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var element = d as TextBlock;
            if (element != null)
            {
                element.SetResourceReference(TextBlock.ForegroundProperty, e.NewValue);
            }
        }

        public static readonly DependencyProperty ForegroundResourceKeyProperty = DependencyProperty.RegisterAttached("ForegroundResourceKey",
            typeof(object),
            typeof(BrushBindingHelper),
            new PropertyMetadata(Brushes.Black , ForegroundResourceKeyChanged));

        public static void SetForegroundResourceKey(TextBlock element, object value)
        {

            element.SetValue(ForegroundResourceKeyProperty, value);
        }

        public static object GetForegroundResourceKey(TextBlock element)
        {

            return element.GetValue(ForegroundResourceKeyProperty);
        }

    }
}
