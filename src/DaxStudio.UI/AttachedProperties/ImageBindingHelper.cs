using System;
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


        public static readonly DependencyProperty SourceAccentResourceKeyProperty = DependencyProperty.RegisterAttached("SourceAccentResourceKey",
                    typeof(object),
                    typeof(ImageBindingHelper),
                    new PropertyMetadata(String.Empty, SourceAccentResourceKeyChanged));

        public static void SetSourceAccentResourceKey(Image element, object value)
        {

            element.SetValue(SourceResourceKeyProperty, value);
        }

        public static object GetSourceAccentResourceKey(Image element)
        {

            return element.GetValue(SourceResourceKeyProperty);
        }

        private static void SourceAccentResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var element = d as Image;
            if (element != null)
            {
                element.SetResourceReference(Image.SourceProperty, e.NewValue.ToString().Replace("DrawingImage","_accentDrawingImage"));
            }
        }

        #region FluentDropDownButton
        public static readonly DependencyProperty IconResourceKeyProperty = DependencyProperty.RegisterAttached("IconResourceKey",
            typeof(object),
            typeof(ImageBindingHelper),
            new PropertyMetadata(String.Empty, IconResourceKeyChanged));

        public static void SetIconResourceKey(Fluent.DropDownButton element, object value)
        {

            element.SetValue(IconResourceKeyProperty, value);
        }

        public static object GetIconResourceKey(Fluent.DropDownButton element)
        {

            return element.GetValue(IconResourceKeyProperty);
        }

        private static void IconResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var button = d as Fluent.DropDownButton;
            if (button != null)
            {
                button.SetResourceReference(Fluent.DropDownButton.IconProperty, e.NewValue);
                button.SetResourceReference(Fluent.DropDownButton.LargeIconProperty, e.NewValue);
            }

        }
        #endregion

        #region ToggleButton
        public static readonly DependencyProperty ToggleIconResourceKeyProperty = DependencyProperty.RegisterAttached("ToggleIconResourceKey",
            typeof(object),
            typeof(ImageBindingHelper),
            new PropertyMetadata(String.Empty, ToggleIconResourceKeyChanged));
        public static void SetToggleIconResourceKey(Fluent.ToggleButton element, object value)
        {

            element.SetValue(IconResourceKeyProperty, value);
        }

        public static object GetToggleIconResourceKey(Fluent.ToggleButton element)
        {

            return element.GetValue(IconResourceKeyProperty);
        }

        private static void ToggleIconResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var button = d as Fluent.ToggleButton;
            if (button != null)
            {
                button.SetResourceReference(Fluent.ToggleButton.IconProperty, e.NewValue);
                button.SetResourceReference(Fluent.ToggleButton.LargeIconProperty , e.NewValue);
            }

        }
        #endregion

        #region FluentButton
        public static readonly DependencyProperty ButtonIconResourceKeyProperty = DependencyProperty.RegisterAttached("ButtonIconResourceKey",
            typeof(object),
            typeof(ImageBindingHelper),
            new PropertyMetadata(String.Empty, ButtonIconResourceKeyChanged));

        public static void SetButtonIconResourceKey(Fluent.Button element, object value)
        {

            element.SetValue(IconResourceKeyProperty, value);
        }

        public static object GetButtonIconResourceKey(Fluent.DropDownButton element)
        {

            return element.GetValue(IconResourceKeyProperty);
        }

        private static void ButtonIconResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var button = d as Fluent.Button;
            if (button != null)
            {
                button.SetResourceReference(Fluent.Button.IconProperty, e.NewValue);
                button.SetResourceReference(Fluent.Button.LargeIconProperty, e.NewValue);
            }

        }


        #endregion
    }
}
