using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DaxStudio.UI.AttachedProperties
{
    public static class DesignModeHelper
    {
        private static bool? inDesignMode;

        public static readonly DependencyProperty VisibilityProperty = DependencyProperty
            .RegisterAttached("Visibility", typeof(Visibility), typeof(DesignModeHelper), new PropertyMetadata(VisibilityChanged));

        private static bool InDesignMode
        {
            get
            {
                if (inDesignMode == null)
                {
                    var prop = DesignerProperties.IsInDesignModeProperty;

                    inDesignMode = (bool)DependencyPropertyDescriptor
                        .FromProperty(prop, typeof(FrameworkElement))
                        .Metadata.DefaultValue;

                    if (!inDesignMode.GetValueOrDefault(false) && Process.GetCurrentProcess().ProcessName.StartsWith("devenv", StringComparison.Ordinal))
                        inDesignMode = true;
                }

                return inDesignMode.GetValueOrDefault(false);
            }
        }

        public static Visibility GetVisibility(DependencyObject dependencyObject)
        {
            return (Visibility)dependencyObject.GetValue(VisibilityProperty);
        }

        public static void SetVisibility(DependencyObject dependencyObject, Visibility value)
        {
            dependencyObject.SetValue(VisibilityProperty, value);
        }

        private static void VisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!InDesignMode)
                return;

            d.SetValue(Control.VisibilityProperty, e.NewValue);
        }
    }
}