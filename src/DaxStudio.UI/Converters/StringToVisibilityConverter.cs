using System;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class StringToVisibilityConverter: IValueConverter
    {
        

            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is string @string &&  string.IsNullOrWhiteSpace(@string))
                {
                    return Visibility.Collapsed;
                }
                return Visibility.Visible;
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        
    }
}
