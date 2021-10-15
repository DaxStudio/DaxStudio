using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.AttachedProperties
{
    public static class ImageBindingHelper
    {
    

        private static void SourceResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var element = d as Image;
            if (element != null)
            {
                element.SetResourceReference(Image.SourceProperty, e.NewValue);
            }
        }

        public static readonly DependencyProperty SourceResourceKeyProperty = DependencyProperty.RegisterAttached("SourceResourceKey",
            typeof(object),
            typeof(ImageBindingHelper),
            new PropertyMetadata(String.Empty, SourceResourceKeyChanged));

        public static void SetSourceResourceKey(Image element, object value)
        {

            element.SetValue(SourceResourceKeyProperty, value);
        }

        public static object GetSourceResourceKey(Image element)
        {

            return element.GetValue(SourceResourceKeyProperty);
        }


        //public static readonly DependencyProperty IconResourceKeyProperty = DependencyProperty.RegisterAttached("IconResourceKey",
        //    typeof(object),
        //    typeof(ImageBindingHelper),
        //    new PropertyMetadata(String.Empty, IconResourceKeyChanged));

        //public static void SetSourceResourceKey(Fluent.DropDownButton element, object value)
        //{

        //    element.SetValue(IconResourceKeyProperty, value);
        //}

        //public static object GetSourceResourceKey(Fluent.DropDownButton element)
        //{

        //    return element.GetValue(IconResourceKeyProperty);
        //}

        //private static void IconResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{

        //    var button = d as Fluent.DropDownButton;
        //    if (button != null)
        //    {
        //        button.SetResourceReference(button.Icon as DependencyProperty, e.NewValue);
        //        button.SetResourceReference(button.LargeIcon as DependencyProperty, e.NewValue);
        //    }

        //}
    }
}
