using System;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class StringToVisibilityConverter: IValueConverter
    {
        

            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                // When invisible, default to Collapsed, but allow callers to reserve layout space by
                // passing ConverterParameter=Hidden (keeps surrounding elements from jumping).
                var whenHidden = string.Equals(parameter as string, "Hidden", StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Hidden
                    : Visibility.Collapsed;

                if (value == null)
                {
                    return whenHidden;
                }
                if (value is string @string &&  string.IsNullOrWhiteSpace(@string))
                {
                    return whenHidden;
                }
                return Visibility.Visible;
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        
    }
}
