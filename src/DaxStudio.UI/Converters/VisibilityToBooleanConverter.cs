using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Converters
{
    [System.Windows.Markup.MarkupExtensionReturnType(typeof(IValueConverter))]
    public class VisbilityToBooleanConverter :  IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}