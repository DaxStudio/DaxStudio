using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.AttachedProperties
{
    public class DataGridColumnVisibilityProperty
    {
        public static bool GetVisibility(DependencyObject obj)
        {
            return (bool)obj.GetValue(VisibilityProperty);
        }
        public static void SetVisibility(DependencyObject obj, bool value)
        {
            obj.SetValue(VisibilityProperty, value);
        }
        public static readonly DependencyProperty VisibilityProperty =
        DependencyProperty.RegisterAttached("Visibility", typeof(bool), typeof(DataGridColumnVisibilityProperty), new PropertyMetadata(true, OnVisibilityChanged));
        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                (d as DataGridColumn).Visibility = Visibility.Visible;
            }
            else
            {
                (d as DataGridColumn).Visibility = Visibility.Collapsed;
            }
        }

    }
}

