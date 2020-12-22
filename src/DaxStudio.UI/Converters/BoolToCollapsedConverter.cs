using System;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class BoolToCollapsedConverter : IValueConverter
    {
    
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Boolean && (bool)value)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility visibility && visibility == Visibility.Visible)
            {
                return true;
            }
            return false;
        }
    }

    class BoolToNotCollapsedConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Boolean && !(bool)value)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility visibility && visibility == Visibility.Visible)
            {
                return true;
            }
            return false;
        }
    }
}

