using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.AttachedProperties
{
    public static class ForegroundBindingHelper
    {

        private static void ResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var element = d as Control;
            if (element != null)
            {
                element.SetResourceReference(Control.ForegroundProperty, e.NewValue);
            }
            else
            {
                var tb = d as TextBlock;
                if (tb != null)
                {
                    tb.SetResourceReference(TextBlock.ForegroundProperty, e.NewValue);
                }
            }
        }

        public static readonly DependencyProperty ResourceKeyProperty = DependencyProperty.RegisterAttached("ResourceKey",
            typeof(object),
            typeof(ForegroundBindingHelper),
            new PropertyMetadata(String.Empty, ResourceKeyChanged));

        public static void SetResourceKey(FrameworkElement element, object value)
        {

            element.SetValue(ResourceKeyProperty, value);
        }

        public static object GetResourceKey(FrameworkElement element)
        {

            return element.GetValue(ResourceKeyProperty);
        }
    }
}
